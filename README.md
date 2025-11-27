# AI Goal Coach — Fullstack (.NET backend + Next.js frontend)

This repository contains a small fullstack prototype that converts vague career aspirations into SMART goals using an LLM-backed backend and a Next.js frontend.

Contents
- `backend/` : .NET Web API implementing the LLM orchestration, simple persistence and telemetry.
- `frontend/` : Next.js app that calls the backend endpoints.

Quickstart

1. Backend:
   - Install .NET 8 SDK
   - Set env vars: `HF_API_TOKEN` (Hugging Face token), optional `HF_MODEL`
   - From repo root: `dotnet restore backend`
   - Run: `ASPNETCORE_URLS=http://localhost:8000 dotnet run --project backend`

2. Frontend:
   - `cd frontend`
   - `npm install`
   - `npm run dev`

**Architecture Decision Record (ADR)**

**Decision**: Used the Hugging Face Router chat-completions endpoint with instruction-based JSON enforcement plus local schema validation.

**Context & Goals**
- We need to convert short, imprecise user inputs into structured SMART goals (`refined_goal`, `key_results`, `confidence_score`).
- The output must be machine-parseable (JSON) so the frontend and storage can rely on a fixed shape.
- The prototype should be cheap to run, easy to iterate on, and portable across model providers.

**Why this model / provider**
- **Router API compatibility**: The Hugging Face Router exposes a chat-completions style API that fits our existing `messages`-based prompt and allows swapping models (Open-source and HF-hosted) with a single parameter (`model`).
- **Experimentation flexibility**: We can try instruction-tuned models (FLAN-T5 family, Llama-variants, etc.) without code-level changes; switching is a config change (`HF_MODEL`).
- **Cost/control trade-off**: Open-source models accessible via HF can be less expensive than proprietary endpoints at scale if self-hosted or used carefully; Router lets us test them before committing to self-hosting.
- **Determinism for refinement tasks**: We choose low temperature and explicit system prompts so outputs are as consistent as possible.

**Why this JSON enforcement method**
- **Approach used**: We enforce JSON by (1) crafting a strict system prompt that instructs the model to output a JSON object matching the required schema, and (2) applying local validation on the backend against the `GoalOutput` DTO (non-empty strings, exactly 3 key results, confidence 1–10).
- **Reasoning**: This approach is provider-agnostic and quick to implement — useful for a prototype and tests.

**Alternatives considered**
- **Function-calling / provider schema enforcement**: More robust and deterministic (OpenAI-style function-calling or provider-side JSON schema enforcement). Trade-off: depends on provider feature parity and would require locking to providers that support it.
- **Constrained decoding / rule-based post-processing**: Safer but costlier to implement; constrained decoding requires deeper integration with model serving infrastructure.

**Trade-offs**
- **Robustness vs. Speed**: Instruction-only enforcement is fast to implement but brittle; we accept some parsing/validation overhead locally to keep iteration speed high.
- **Cost vs. Quality**: Larger models give higher quality but cost more. The prototype uses low temperature and strict prompts to reduce retries, but at scale we'll need caching and model-routing.
- **Simplicity vs. Scalability**: Currently synchronous call patterns are simplest to reason about; at 10k users we must move to asynchronous queueing and autoscaling.

**Scaling to 10,000 users — concrete plan**
- **Queue + Worker Model**: Decouple incoming HTTP requests from LLM calls with a job queue (Redis/RabbitMQ/Azure Service Bus). Workers process jobs and call the LLM. This enables retries, backoff, and autoscaling of workers.
- **Caching**: Cache responses for identical prompts (hash prompt+model+config) in Redis to avoid duplicate LLM calls.
- **Adaptive Model Routing**: Use a small, cheap model for initial classification/refinement; route to a larger model only when higher quality is needed.
- **Rate-limiting & Throttling**: Enforce per-user and global rate limits to protect the provider and control cost.
- **Batching & Backpressure**: Batch requests where applicable and apply backpressure when provider limits are reached.
- **Autoscaling inference**: For heavy load, either rely on provider-managed autoscaling or deploy self-hosted inference clusters (GPU autoscaling, model shards).

**Operational & Security considerations**
- Store `HF_API_TOKEN` and other secrets in a secrets manager (do not commit to source control).
- Use structured logs with telemetry for requests, tokens, latency, and parse success rates.
- Enforce CORS and tighten allowed origins in production.
- Add monitoring and alerts on token consumption and parse-failure rates.




