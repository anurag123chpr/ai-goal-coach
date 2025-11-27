namespace AiGoalCoach.Api.Models
{
    using System;

    /// <summary>
    /// Normalized result returned by an <see cref="Services.ILlmService"/> implementation.
    /// Contains both the raw text from the provider and the parsed <see cref="GoalOutput"/> when available.
    /// </summary>
    public class ModelResult
    {
        /// <summary>
        /// Gets or sets the raw response text returned by the provider (may contain prose and JSON).
        /// </summary>
        public string Raw { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the parsed <see cref="GoalOutput"/> if parsing and validation succeeded; otherwise null.
        /// </summary>
        public GoalOutput? Parsed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the parsed result passed validation.
        /// </summary>
        public bool ParseOk { get; set; }

        /// <summary>
        /// Gets or sets the round-trip latency in milliseconds for the model call.
        /// </summary>
        public int LatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the approximate prompt token count (simple word count fallback).
        /// </summary>
        public int PromptTokens { get; set; }

        /// <summary>
        /// Gets or sets the approximate completion token count (simple word count fallback).
        /// </summary>
        public int CompletionTokens { get; set; }
    }
}
