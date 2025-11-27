# AI Goal Coach — Fullstack (.NET backend + Next.js frontend)

This repository contains a fullstack prototype that converts vague career aspirations into SMART goals using an LLM-backed backend and a Next.js frontend.

## Contents

- `backend/` —  .NET Web API implementing the LLM orchestration, simple persistence and telemetry.
- `frontend/` — Next.js app that calls the backend endpoints

## Quickstart

### Backend

1. Install .NET 8 SDK
2. Set environment variables:
   ```powershell
   $env:HF_API_TOKEN = "your-hf-api-token"
   $env:HF_MODEL = "openai/gpt-4-turbo"  # Optional, defaults to a small model
   $env:HF_BASE_URL = "https://router.huggingface.co/"  # Optional, configurable for testing
   $env:HF_TIMEOUT_SECONDS = "30"  # Optional, request timeout
   ```
3. From repo root:
   ```powershell
   dotnet restore backend
   $env:ASPNETCORE_URLS = "http://localhost:8000"; dotnet run --project backend
   ```

### Frontend

```powershell
cd frontend
npm install
npm run dev
```

Frontend runs at `http://localhost:3000`

### Running Tests

```powershell
dotnet test backend/AiGoalCoach.Tests/AiGoalCoach.Tests.csproj
```

## Architecture

### Service Abstraction Layer

The backend uses an `ILlmService` interface to abstract LLM provider logic:

```csharp
public interface ILlmService
{
    Task<ModelResult> RefineGoalAsync(string input, CancellationToken cancellationToken = default);
}
```

**Implementation**: `HuggingFaceService` — orchestrates calls to the Hugging Face Router API with:
- Structured request/response handling (messages-based chat completions)
- JSON parsing and validation against the `GoalOutput` schema
- Structured telemetry logging (latency, tokens, parse success rate)

**Result DTO**: `ModelResult` — normalized across all providers:
```csharp
public class ModelResult
{
    public string Raw { get; set; }
    public GoalOutput Parsed { get; set; }
    public bool ParseOk { get; set; }
    public int LatencyMs { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
}
```

### Resilience & Retries (Polly)

The typed `HttpClient` for Hugging Face is registered with a **Polly retry policy**:
- **Retries**: 3 attempts with exponential backoff ($2^n$ seconds)
- **Triggers**: 5xx errors, network timeouts, HTTP 429 (rate limiting)
- **Configuration**: Read from environment:
  - `HF_TIMEOUT_SECONDS` — request timeout (default: 30s)
  - `HF_BASE_URL` — LLM endpoint (default: `https://router.huggingface.co/`)

This reduces transient failures and makes the system more resilient to provider blips.

### Persistence Layer

The backend uses an `IGoalRepository` abstraction for persistence:

```csharp
public interface IGoalRepository
{
    Task SaveAsync(GoalOutput goal);
    Task<List<GoalOutputEntry>> ListAsync();
}
```

**Implementation**: `FileGoalRepository` — stores refined goals as JSON to `backend/data/saved_goals.json`

This abstraction allows swapping file-based storage with a database later without changing the controller.

### Dependency Injection

Services are registered in `Program.cs`:
```csharp
builder.Services.AddHttpClient<ILlmService, HuggingFaceService>(client =>
{
    client.BaseAddress = new Uri(hfBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(hfTimeoutSeconds);
})
    .AddPolicyHandler(GetDefaultRetryPolicy());

builder.Services.AddScoped<IGoalRepository, FileGoalRepository>();
builder.Services.AddSingleton<TelemetryService>();
```

### Configuration Keys

| Key | Default | Purpose |
|-----|---------|---------|
| `HF_API_TOKEN` | (required) | Hugging Face API authentication token |
| `HF_MODEL` | (required) | Model identifier (e.g., `openai/gpt-4-turbo`) |
| `HF_BASE_URL` | `https://router.huggingface.co/` | LLM endpoint (configurable for testing or provider swaps) |
| `HF_TIMEOUT_SECONDS` | `30` | HTTP request timeout |
| `HF_PROMPT_PATH` | `config/prompts/system_prompt_v1.txt` | Path to system prompt template |

## API Endpoints

### POST `/api/goals/refine`
Refines a vague goal into a SMART goal.

**Request:**
```json
{
  "input": "I want to get better at sales"
}
```

**Response:**
```json
{
  "refined_goal": "Increase sales closing rate from 20% to 30% within 6 months by implementing daily cold-calling practice and objection-handling drills.",
  "key_results": [
    "Complete 50 cold calls per week with documented outcomes",
    "Practice 3 objection-handling scenarios weekly with a mentor",
    "Increase deal-close rate by 2% each month (measured via CRM)"
  ],
  "confidence_score": 8
}
```

### POST `/api/goals/save`
Saves a refined goal to persistent storage.

### GET `/api/goals/list`
Returns all saved goals.

## Architecture Decision Record (ADR)

### Decision
Use the Hugging Face Router chat-completions endpoint with instruction-based JSON enforcement, local schema validation, and resilience through Polly retries.

### Context & Goals
- Convert short, imprecise user inputs into structured SMART goals (`refined_goal`, `key_results`, `confidence_score`)
- Output must be machine-parseable (JSON) for frontend/storage reliability
- Prototype should be cost-effective, iterable, and portable across LLM providers

### Why This Approach

**Provider Choice: Hugging Face Router**
- **Chat-completions compatibility**: Standard API reduces vendor lock-in; easy model swaps via config
- **Model flexibility**: Test instruction-tuned models (FLAN-T5, Llama, etc.) without code changes
- **Cost-effective**: Open-source models reduce costs vs. proprietary endpoints
- **Determinism**: Low temperature + explicit prompts ensure consistent outputs

**JSON Enforcement**
- **Method**: Strict system prompt + local validation against `GoalOutput` schema
- **Advantages**: Provider-agnostic, quick to implement, suitable for prototypes
- **Trade-off**: Brittleness vs. speed (acceptable for early stages)

**Resilience: Polly Retries**
- **Exponential backoff**: Reduces thundering herd on provider endpoints
- **Rate-limit handling**: Automatically retries on HTTP 429
- **Transient error recovery**: Handles network blips and 5xx errors gracefully

### Future Scaling (10,000+ users)

- **Async Queue**: Decouple HTTP requests from LLM calls (Redis/RabbitMQ/Service Bus)
- **Caching**: Redis cache for identical prompt responses
- **Model Routing**: Lightweight model for classification; larger model for complex refinement
- **Rate Limiting**: Per-user and global limits to manage costs
- **Autoscaling**: Worker pools or provider-managed autoscaling
- **Database**: Replace file-based storage with PostgreSQL/MongoDB

## Security & Operations

- **Secrets Management**: Store `HF_API_TOKEN` in a secrets manager; never commit to source control
- **Structured Logging**: Telemetry tracks latency, tokens, parse success, and errors
- **CORS**: Configured for local dev (`http://localhost:3000`); tighten in production
- **Monitoring**: Add alerts on token consumption, parse-failure rates, and retry counts
- **Rate Limiting**: Implement per-user quotas to prevent abuse
