namespace AiGoalCoach.Api.Repositories
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AiGoalCoach.Api.Models;

    /// <summary>
    /// Abstraction for persisting and listing saved goals.
    /// </summary>
    public interface IGoalRepository
    {
        /// <summary>
        /// Saves a refined goal to persistent storage.
        /// </summary>
        /// <param name="output">The refined <see cref="GoalOutput"/> to persist.</param>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        Task SaveAsync(GoalOutput output);

        /// <summary>
        /// Lists all persisted goal entries.
        /// </summary>
        /// <returns>A read-only list of saved goal entries.</returns>
        Task<IReadOnlyList<GoalOutputEntry>> ListAsync();
    }
}
