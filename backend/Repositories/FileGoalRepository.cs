namespace AiGoalCoach.Api.Repositories
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AiGoalCoach.Api.Models;

    /// <summary>
    /// File-backed repository implementation that persists goals to `data/saved_goals.json`.
    /// </summary>
    public class FileGoalRepository : IGoalRepository
    {
        private readonly string savePath;
        private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        /// <summary>
        /// Initializes a new instance of the <see cref="FileGoalRepository"/> class.
        /// </summary>
        /// <param name="savePath">Optional path to the save file. If null, defaults to `data/saved_goals.json` under app base.</param>
        public FileGoalRepository(string? savePath = null)
        {
            var path = savePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
                Directory.CreateDirectory(dataDir);
                path = Path.Combine(dataDir, "saved_goals.json");
            }

            this.savePath = path!;
        }

        /// <inheritdoc />
        public async Task SaveAsync(GoalOutput output)
        {
            var list = new List<GoalOutputEntry>();
            if (File.Exists(this.savePath))
            {
                try
                {
                    var txt = await File.ReadAllTextAsync(this.savePath);
                    list = JsonSerializer.Deserialize<List<GoalOutputEntry>>(txt) ?? new List<GoalOutputEntry>();
                }
                catch
                {
                    list = new List<GoalOutputEntry>();
                }
            }

            var entry = new GoalOutputEntry
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                RefinedGoal = output.RefinedGoal,
                KeyResults = output.KeyResults ?? new List<string>(),
                ConfidenceScore = output.ConfidenceScore,
                SavedAt = System.DateTime.UtcNow.ToString("o"),
            };

            list.Add(entry);
            var serialized = JsonSerializer.Serialize(list, this.jsonOptions);
            await File.WriteAllTextAsync(this.savePath, serialized);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<GoalOutputEntry>> ListAsync()
        {
            if (!File.Exists(this.savePath))
            {
                return new List<GoalOutputEntry>();
            }

            try
            {
                var txt = await File.ReadAllTextAsync(this.savePath);
                var list = JsonSerializer.Deserialize<List<GoalOutputEntry>>(txt) ?? new List<GoalOutputEntry>();
                return list.OrderByDescending(i => i.Id).ToList();
            }
            catch
            {
                return new List<GoalOutputEntry>();
            }
        }
    }
}
