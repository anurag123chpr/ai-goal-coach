namespace AiGoalCoach.Api.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Data Transfer Object (DTO) representing a refined SMART goal.
    /// Matches the JSON schema enforced by the Hugging Face LLM system prompt.
    /// </summary>
    /// <remarks>
    /// SMART = Specific, Measurable, Achievable, Relevant, Time-bound.
    /// This object is produced by the LLM and validated by <see cref="Services.HuggingFaceService"/>.
    /// </remarks>
    public class GoalOutput
    {
        /// <summary>
        /// Gets or sets the refined goal statement, made specific and actionable by the LLM.
        /// </summary>
        /// <remarks>
        /// Must be 10–1000 characters, non-empty, and describe a measurable outcome.
        /// Example: "Complete AWS Solutions Architect certification within 6 months.".
        /// </remarks>
        [Required]
        [MinLength(10)]
        [MaxLength(1000)]
        [JsonPropertyName("refined_goal")]
        public string RefinedGoal { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a list of exactly 3 measurable key results (OKRs) that define success for this goal.
        /// </summary>
        /// <remarks>
        /// Each key result must be non-empty and quantifiable.
        /// Examples:
        /// - "Pass the AWS Solutions Architect exam by Q2 2025"
        /// - "Build 2 production-ready AWS projects using EC2, S3, and Lambda"
        /// - "Complete at least 10 hours of study per week for 24 weeks.".
        /// </remarks>
        [Required]
        [JsonPropertyName("key_results")]
        public List<string> KeyResults { get; set; } = new();

        /// <summary>
        /// Gets or sets the confidence score (1–10) indicating how likely the goal is to be achieved.
        /// </summary>
        /// <remarks>
        /// 1 = very unlikely (vague or unfeasible input)
        /// 5 = moderate confidence
        /// 10 = highly likely (clear, specific, realistic goal)
        ///
        /// The LLM assigns this based on goal clarity, feasibility, and the user's input quality.
        /// </remarks>
        [Range(1, 10)]
        [JsonPropertyName("confidence_score")]
        public int ConfidenceScore { get; set; }
    }
}
