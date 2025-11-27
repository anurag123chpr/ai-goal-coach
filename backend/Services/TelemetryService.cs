namespace AiGoalCoach.Api.Services
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Service for logging structured telemetry data (call metadata) to a file and application logs.
    /// </summary>
    /// <remarks>
    /// Writes JSON-formatted telemetry entries to both:
    /// - The .NET application log via <see cref="ILogger"/> (for real-time monitoring)
    /// - A persistent JSON file (for post-analysis and archival)
    ///
    /// Configuration keys (read from <see cref="IConfiguration"/>):
    /// - TELEMETRY_LOG: Path to telemetry log file (default: &lt;ContentRootPath&gt;/logs/ai_calls.log).
    /// </remarks>
    public class TelemetryService
    {
        private readonly ILogger<TelemetryService> log;
        private readonly string logFile;

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryService"/> class.
        /// </summary>
        /// <param name="log">Logger for diagnostics if file I/O fails.</param>
        /// <param name="cfg">Configuration provider for reading the custom telemetry log path.</param>
        /// <param name="env">Host environment to resolve the content root path for the default logs directory.</param>
        /// <remarks>
        /// Ensures the log directory exists and configures the log file path.
        /// </remarks>
        public TelemetryService(ILogger<TelemetryService> log, IConfiguration cfg, IHostEnvironment env)
        {
            this.log = log;
            var defaultLogDir = Path.Combine(env.ContentRootPath ?? AppContext.BaseDirectory, "logs");
            this.logFile = cfg["TELEMETRY_LOG"] ?? Path.Combine(defaultLogDir, "ai_calls.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(this.logFile) ?? defaultLogDir);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Logs a structured telemetry entry (call metadata) to the application log and persistent telemetry file.
        /// </summary>
        /// <param name="entry">An anonymous object or strongly-typed instance containing telemetry fields (e.g., request_id, model, latency_ms, etc.).</param>
        /// <remarks>
        /// This method is fire-and-forget: exceptions during file I/O are caught and logged but do not propagate.
        /// Typical entry format:
        /// <code>
        /// new {
        ///     ts = DateTime.UtcNow.ToString("o"),
        ///     request_id = Guid.NewGuid().ToString(),
        ///     model = "model-name",
        ///     latency_ms = 500,
        ///     prompt_tokens = 100,
        ///     completion_tokens = 50,
        ///     parsed = true
        /// }
        /// </code>
        /// </remarks>
        public void LogCall(object entry)
        {
            try
            {
                var line = JsonSerializer.Serialize(entry);
                this.log.LogInformation(line);
                File.AppendAllText(this.logFile, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                this.log.LogError(ex, "Failed to write telemetry");
            }
        }
    }
}
