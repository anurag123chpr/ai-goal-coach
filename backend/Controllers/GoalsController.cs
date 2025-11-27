// <copyright file="GoalsController.cs" company="AiGoalCoach">
// Copyright (c) All rights reserved.
// </copyright>

namespace AiGoalCoach.Api.Controllers
{
    using System.Text.Json;
    using AiGoalCoach.Api.Models;
    using AiGoalCoach.Api.Services;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// REST API endpoints for goal refinement and management.
    /// Provides endpoints to:
    /// - Refine vague goals into SMART goals using the Hugging Face LLM
    /// - Save refined goals to persistent storage
    /// - Retrieve previously saved goals.
    /// </summary>
    /// <remarks>
    /// All endpoints require the backend to be running and configured with HF_API_TOKEN.
    /// Goals are persisted to a JSON file under the "data/" directory.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    public class GoalsController : ControllerBase
    {
        private readonly ILlmService hf;
        private readonly TelemetryService tele;
        private readonly IConfiguration cfg;
        private readonly AiGoalCoach.Api.Repositories.IGoalRepository repo;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoalsController"/> class.
        /// </summary>
        /// <param name="hf">Service for refining goals via the Hugging Face API.</param>
        /// <param name="tele">Service for logging telemetry.</param>
        /// <param name="cfg">Configuration provider for reading model and API settings.</param>
        /// <param name="repo">Repository for persisting and listing saved goals.</param>
        /// <remarks>
        /// Ensures the data directory exists for persisting goals.
        /// </remarks>
        public GoalsController(ILlmService hf, TelemetryService tele, IConfiguration cfg, AiGoalCoach.Api.Repositories.IGoalRepository repo)
        {
            this.hf = hf;
            this.tele = tele;
            this.repo = repo;
            this.cfg = cfg;
        }

        /// <summary>
        /// Refines a vague goal input into a structured SMART goal.
        /// </summary>
        /// <param name="body">JSON body containing "text" field with the goal input (e.g., { "text": "I want to learn AWS" }).</param>
        /// <returns>
        /// - 400 BadRequest if input text is empty.
        /// - 200 OK with { ok: true, data: GoalOutput, latency_ms: int } on success.
        /// - 200 OK with { ok: false, reason: "parse_failed" } if parsing/validation fails.
        /// </returns>
        /// <remarks>
        /// This endpoint:
        /// 1. Validates that input text is provided and non-empty
        /// 2. Calls <see cref="HuggingFaceService.RefineAsync"/> to generate a SMART goal
        /// 3. Logs call metadata (model, latency, tokens, parse success) via <see cref="TelemetryService"/>
        /// 4. Returns the refined goal or a parse-failure error response
        ///
        /// Response times depend on the LLM provider and input complexity (typically 1–5 seconds).
        /// </remarks>
        [HttpPost("refine")]
        public async Task<IActionResult> Refine([FromBody] JsonElement body)
        {
            if (!body.TryGetProperty("text", out var textProp) || string.IsNullOrWhiteSpace(textProp.GetString()))
            {
                return this.BadRequest(new { error = "Input text is empty.", });
            }

            var text = textProp.GetString() ?? string.Empty;
            var requestId = Guid.NewGuid().ToString();

            var result = await this.hf.RefineGoalAsync(text);

            var telemetryEntry = new
            {
                request_id = requestId,
                model = this.cfg["HF_MODEL"],
                input = text.Trim(),
                output = result.Parsed,
                latency_ms = result.LatencyMs,
                prompt_tokens = result.PromptTokens,
                completion_tokens = result.CompletionTokens,
                ts = DateTime.UtcNow.ToString("o"),
            };
            this.tele.LogCall(telemetryEntry);

            if (!result.ParseOk || result.Parsed == null)
            {
                return this.Ok(new { ok = false, reason = "parse_failed", confidence_score = 1, });
            }

            return this.Ok(new { ok = true, data = result.Parsed, latency_ms = result.LatencyMs, });
        }

        /// <summary>
        /// Saves a refined goal to persistent storage.
        /// </summary>
        /// <param name="payload">A <see cref="GoalOutput"/> object to save (will be persisted with a timestamp and auto-generated ID).</param>
        /// <returns>
        /// 200 OK with { ok: true, entry: { id, refined_goal, key_results, confidence_score, saved_at } } on success.
        /// </returns>
        /// <remarks>
        /// Each goal is assigned a Unix timestamp as its ID and stored in a JSON array in the data directory.
        /// Goals are appended; existing goals are preserved.
        /// </remarks>
        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] GoalOutput payload)
        {
            // Persist via repository
            await this.repo.SaveAsync(payload);
            var entry = new
            {
                id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                refined_goal = payload.RefinedGoal,
                key_results = payload.KeyResults,
                confidence_score = payload.ConfidenceScore,
                saved_at = DateTime.UtcNow.ToString("o"),
            };
            return this.Ok(new { ok = true, entry, });
        }

        /// <summary>
        /// Retrieves all previously saved goals.
        /// </summary>
        /// <returns>
        /// 200 OK with { ok: true, items: [ { id, refined_goal, key_results, confidence_score, saved_at }, ... ] }.
        /// Returns an empty array if no goals have been saved.
        /// </returns>
        /// <remarks>
        /// Goals are read from the persistent JSON file in the data directory.
        /// If the file does not exist, an empty list is returned.
        /// </remarks>
        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            var items = await this.repo.ListAsync();
            var mapped = items.Select(i => new
            {
                id = i.Id,
                refined_goal = i.RefinedGoal,
                key_results = i.KeyResults,
                confidence_score = i.ConfidenceScore,
                saved_at = i.SavedAt,
            }).ToList();

            return this.Ok(new { ok = true, items = mapped, });
        }
    }
}
