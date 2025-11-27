namespace AiGoalCoach.Api.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using AiGoalCoach.Api.Models;

    /// <summary>
    /// Abstraction over an LLM model provider.
    /// Implementations encapsulate provider-specific request/response details
    /// and return a normalized <see cref="ModelResult"/>.
    /// </summary>
    public interface ILlmService
    {
        /// <summary>
        /// Generate a model result from the provided system + user prompt.
        /// </summary>
        /// <param name="input">User input text to refine.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="ModelResult"/> containing raw and parsed outputs and metadata.</returns>
        Task<ModelResult> RefineGoalAsync(string input, CancellationToken cancellationToken = default);
    }
}
