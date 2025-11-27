namespace AiGoalCoach.Api.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A persisted representation of a refined goal including metadata.
    /// </summary>
    public class GoalOutputEntry
    {
        /// <summary>
        /// Gets or sets the unique identifier (Unix milliseconds) for the saved entry.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the refined goal text.
        /// </summary>
        public string RefinedGoal { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the key results associated with the goal.
        /// </summary>
        public List<string> KeyResults { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the confidence score for the refined goal.
        /// </summary>
        public int ConfidenceScore { get; set; }

        /// <summary>
        /// Gets or sets the ISO-8601 timestamp when the entry was saved.
        /// </summary>
        public string SavedAt { get; set; } = DateTime.UtcNow.ToString("o");
    }
}
