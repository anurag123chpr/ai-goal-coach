// <copyright file="Program.cs" company="AiGoalCoach">
// Copyright (c) All rights reserved.
// </copyright>

using AiGoalCoach.Api.Repositories;
using AiGoalCoach.Api.Services;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
       .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
       .AddEnvironmentVariables();
builder.Logging.AddConsole();

// Read configuration for HF HTTP client (timeouts, base URL)
var hfTimeoutSeconds = 30;
if (int.TryParse(builder.Configuration["HF_TIMEOUT_SECONDS"], out var parsed))
{
    hfTimeoutSeconds = parsed;
}

var hfBaseUrl = builder.Configuration["HF_BASE_URL"] ?? "https://router.huggingface.co/";

// Register services

// Register a typed HttpClient for Hugging Face and attach a Polly retry policy to reduce transient failures.
builder.Services.AddHttpClient<ILlmService, HuggingFaceService>(client =>
{
    client.BaseAddress = new Uri(hfBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(hfTimeoutSeconds);

    // Default headers (Authorization is set inside the service from configuration to allow token updates if needed)
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AiGoalCoach/1.0");
})
    .AddPolicyHandler(GetDefaultRetryPolicy());

builder.Services.AddSingleton<TelemetryService>();

// Repository for saved goals
builder.Services.AddScoped<IGoalRepository, FileGoalRepository>();

// MVC controllers
builder.Services.AddControllers();

// CORS for local dev
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors();
app.MapControllers();
app.Run();

/// <summary>
/// Program class for DI and application setup.
/// </summary>
public partial class Program
{
    private static IAsyncPolicy<HttpResponseMessage> GetDefaultRetryPolicy()
    {
        // Retry on transient errors (5xx, network failures) and 429 (rate limiting).
        // Uses exponential backoff with a small jitter.
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => (int)msg.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    Console.WriteLine($"Delaying for {timespan.TotalSeconds} seconds, then making retry {retryAttempt}.");
                });
    }
}
