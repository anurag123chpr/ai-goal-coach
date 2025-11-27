// <copyright file="HuggingFaceService.cs" company="AiGoalCoach">
// Copyright (c) All rights reserved.
// </copyright>

namespace AiGoalCoach.Api.Services
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using AiGoalCoach.Api.Models;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Service that orchestrates calls to the Hugging Face Router API to refine vague goals into SMART goals.
    /// Handles HTTP communication, response parsing, validation, and telemetry logging.
    /// </summary>
    /// <remarks>
    /// This service:
    /// - Loads a versioned system prompt from disk or uses an embedded fallback
    /// - Sends goal refinement requests to the configured LLM endpoint (defaults to Hugging Face Router)
    /// - Parses JSON responses and validates them against the <see cref="GoalOutput"/> schema
    /// - Logs call metrics (latency, tokens, parse success) to telemetry
    ///
    /// Configuration keys (read from <see cref="IConfiguration"/>):
    /// - HF_API_TOKEN: Hugging Face API authentication token (required)
    /// - HF_MODEL: Model identifier to use (e.g., "openai/gpt-oss-20b"; required)
    /// - HF_BASE_URL: Base URL for the LLM endpoint (default: "https://router.huggingface.co/"; configurable for testing or provider swaps)
    /// - HF_TIMEOUT_SECONDS: HTTP request timeout in seconds (default: 30)
    /// - HF_PROMPT_PATH: Relative path to prompt template file (default: "config/prompts/system_prompt_v1.txt").
    /// </remarks>
    public class HuggingFaceService : ILlmService
    {
        private readonly HttpClient httpClient;
        private readonly IConfiguration cfg;
        private readonly ILogger<HuggingFaceService> log;
        private readonly string model;
        private readonly string token;
        private readonly string systemPrompt;

        /// <summary>
        /// Initializes a new instance of the <see cref="HuggingFaceService"/> class.
        /// Loads configuration settings and attempts to load the system prompt from disk.
        /// </summary>
        /// <param name="httpClient">The typed <see cref="HttpClient"/> configured for Hugging Face calls.</param>
        /// <param name="cfg">Configuration provider for reading API keys, model, timeouts, etc.</param>
        /// <param name="log">Logger for application diagnostics and telemetry.</param>
        /// <param name="env">Host environment to resolve content root path for prompt file loading.</param>
        /// <remarks>
        /// If the prompt file cannot be loaded, a fallback embedded prompt is used.
        /// Check logs for warnings if the prompt file is missing.
        /// </remarks>
        public HuggingFaceService(HttpClient httpClient, IConfiguration cfg, ILogger<HuggingFaceService> log, IHostEnvironment env)
        {
            this.httpClient = httpClient;
            this.cfg = cfg;
            this.log = log;
            this.model = cfg["HF_MODEL"] ?? string.Empty;
            this.token = cfg["HF_API_TOKEN"] ?? string.Empty;

            // If a token is configured, set a default Authorization header on the typed HttpClient.
            try
            {
                if (!string.IsNullOrEmpty(this.token))
                {
                    // Replace any existing Authorization header
                    this.httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", this.token);
                }
            }
            catch (Exception ex)
            {
                this.log.LogWarning(ex, "Failed to set default authorization header on HF HttpClient");
            }

            // Load prompt from configured path, fall back to embedded default if missing
            var promptPath = cfg["HF_PROMPT_PATH"] ?? "config/prompts/system_prompt_v1.txt";
            try
            {
                var abs = Path.Combine(env.ContentRootPath ?? AppContext.BaseDirectory, promptPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(abs))
                {
                    this.systemPrompt = File.ReadAllText(abs);
                }
                else
                {
                    this.log.LogWarning("Prompt file not found at {Path}, falling back to embedded prompt.", abs);
                    this.systemPrompt = string.Empty;
                }
            }
            catch (Exception ex)
            {
                this.log.LogError(ex, "Failed to load system prompt file");
                this.systemPrompt = string.Empty;
            }
        }

        /// <summary>
        /// Asynchronously refines a vague goal input into a structured SMART goal via the Hugging Face Router API.
        /// </summary>
        /// <param name="input">The user's goal input text (e.g., "I want to get better at sales").</param>
        /// <returns>
        /// A tuple containing:
        /// - ok: True if parsing succeeded and output passes validation; otherwise false.
        /// - raw: The raw response text from the HF Router (may contain prose, reasoning, or JSON).
        /// - parsed: The deserialized <see cref="GoalOutput"/> if validation succeeded; otherwise null.
        /// - latencyMs: Round-trip latency in milliseconds.
        /// - promptTokens: Approximate token count of the user input.
        /// - completionTokens: Approximate token count of the model output.
        /// </returns>
        /// <remarks>
        /// This method:
        /// 1. Sends the input + system prompt to the HF Router endpoint
        /// 2. Extracts the model's response from the router JSON envelope
        /// 3. Attempts to deserialize and validate against the <see cref="GoalOutput"/> schema
        /// 4. Logs call metadata (latency, tokens, success) to telemetry
        ///
        /// If the LLM output fails JSON parsing or validation, ok=false but the method does not throw.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Not thrown; all errors are caught and logged.</exception>
        public virtual async Task<ModelResult> RefineAsync(string input)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string generated = string.Empty;
            string rawResponse = string.Empty;

            try
            {
                // Use the injected typed HttpClient to call the Hugging Face Router chat completions API
                var http = this.httpClient;

                // Use the Hugging Face Router chat completions API (relative to typed client's BaseAddress)
                var url = "v1/chat/completions";

                var systemMessage = this.systemPrompt;
                var payload = new
                {
                    model = this.model,
                    messages = new[]
                    {
                        new { role = "system", content = systemMessage, },
                        new { role = "user", content = input, },
                    },
                    max_tokens = 400,
                    temperature = 0.0,
                };

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var resp = await http.PostAsync(url, content);
                resp.EnsureSuccessStatusCode();
                rawResponse = await resp.Content.ReadAsStringAsync();

                try
                {
                    using var jdoc = JsonDocument.Parse(rawResponse);

                    // Expected router response: { choices: [ { message: { role: "assistant", content: "..." } } ] }
                    if (jdoc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var first = choices[0];
                        if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentProp))
                        {
                            generated = contentProp.GetString() ?? string.Empty;
                        }
                        else if (first.TryGetProperty("text", out var textProp))
                        {
                            generated = textProp.GetString() ?? string.Empty;
                        }
                        else
                        {
                            generated = first.ToString();
                        }
                    }
                    else
                    {
                        // Fallback: try to extract 'generated_text' or 'text'
                        if (jdoc.RootElement.TryGetProperty("generated_text", out var gt))
                        {
                            generated = gt.GetString() ?? string.Empty;
                        }
                        else if (jdoc.RootElement.TryGetProperty("text", out var t))
                        {
                            generated = t.GetString() ?? string.Empty;
                        }
                        else
                        {
                            generated = rawResponse;
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.log.LogError(ex, "Unexpected error during parsing HF router response");
                    generated = rawResponse;
                }
            }
            catch (Exception ex)
            {
                this.log.LogError(ex, "Unexpected error during router HTTP call");
                rawResponse = ex.ToString();
                generated = string.Empty;
            }

            sw.Stop();
            int latency = (int)sw.ElapsedMilliseconds;
            int promptTokens = input.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            int completionTokens = generated.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

            GoalOutput? parsed = null;
            bool parseOk = false;
            if (!string.IsNullOrWhiteSpace(generated))
            {
                try
                {
                    parsed = JsonSerializer.Deserialize<GoalOutput>(generated, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsed != null
                        && !string.IsNullOrWhiteSpace(parsed.RefinedGoal)
                        && parsed.RefinedGoal.Length >= 10
                        && parsed.KeyResults?.Count == 3
                        && parsed.KeyResults.All(kr => !string.IsNullOrWhiteSpace(kr))
                        && parsed.ConfidenceScore >= 1
                        && parsed.ConfidenceScore <= 10)
                    {
                        parseOk = true;
                    }
                    else
                    {
                        this.log.LogDebug(
                            "Generated output failed validation: RefinedGoal={RefinedGoal}, KeyResultsCount={KeyResultsCount}, ConfidenceScore={ConfidenceScore}",
                            parsed?.RefinedGoal ?? "(null)",
                            parsed?.KeyResults?.Count ?? 0,
                            parsed?.ConfidenceScore ?? 0);
                    }
                }
                catch (Exception ex)
                {
                    this.log.LogDebug(ex, "Failed to parse generated text as GoalOutput");
                    parseOk = false;
                }
            }

            var entry = new
            {
                ts = DateTime.UtcNow.ToString("o"),
                input = input.Length > 200 ? input.Substring(0, 200) + "..." : input,
                model = this.model,
                latency_ms = latency,
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                parsed = parseOk,
            };
            this.log.LogInformation(JsonSerializer.Serialize(entry));

            return new ModelResult
            {
                Raw = rawResponse,
                Parsed = parsed,
                ParseOk = parseOk,
                LatencyMs = latency,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
            };
        }

        /// <summary>
        /// ILlmService implementation - returns a normalized <see cref="Models.ModelResult"/>.
        /// </summary>
        /// <param name="input">The user's goal input text to refine.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A <see cref="Models.ModelResult"/> containing the raw provider response and any parsed <see cref="GoalOutput"/>.</returns>
        public async Task<ModelResult> RefineGoalAsync(string input, System.Threading.CancellationToken cancellationToken = default)
        {
            // Reuse the provider flow which already returns a normalized ModelResult.
            return await this.RefineAsync(input);
        }
    }
}
