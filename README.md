# RAG vs Agentic RAG

Same dataset, same questions, two endpoints: `/rag/naive` and `/rag/agentic`.
A side-by-side comparison of classic linear RAG and agentic RAG, built with
.NET 9 Minimal API, Microsoft.Extensions.AI, Qdrant (hybrid dense + sparse
search) and MongoDB.

Companion repo for my article **"RAG vs Agentic RAG: when retrieval is no
longer enough"** on [cool-solution.com](https://cool-solution.com).

## The two pipelines

```
NAIVE                                  AGENTIC
─────                                  ───────
question                               question
   │                                      │
   ▼                                      ▼
embed query                            LLM decides ──────────────┐
   │                                      │                      │
   ▼                                      ▼                      │
hybrid search (once)                   search_docs (1..n times)  │
   │                                      │                      │
   ▼                                      ▼                      │
stuff chunks in prompt                 rerank_evaluate           │
   │                                      │                      │
   ▼                                   INSUFFICIENT? ────────────┤
answer                                    │ refine_query, retry  │
                                          ▼                      │
                                       SUFFICIENT ◄──────────────┘
                                          │
                                          ▼
                                       answer (with citations)
```

The naive pipeline retrieves **once** and answers with whatever came back.
The agentic pipeline gives the LLM three tools — `search_docs`,
`rerank_evaluate`, `refine_query` — and lets it drive: split the question
into sub-queries, judge the evidence, retry with better queries when the
first retrieval was not enough. The loop is native
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)
function invocation — no framework on top.

## Quickstart

Prerequisites: .NET 9 SDK, Docker, [Ollama](https://ollama.com) (default
provider — runs fully local and free).

```bash
docker compose up -d
ollama pull llama3.1:8b && ollama pull nomic-embed-text
dotnet run --project src/RagVsAgenticRag.Api
```

Then, in another terminal:

```bash
# 1. ingest the demo dataset (fictional company docs in data/docs/)
curl -X POST http://localhost:5210/admin/seed

# 2. ask the same question to both pipelines
curl -X POST http://localhost:5210/rag/naive \
  -H "Content-Type: application/json" \
  -d '{"question": "Compare the 2024 and 2025 return policies: what changed for opened items?"}'

curl -X POST http://localhost:5210/rag/agentic \
  -H "Content-Type: application/json" \
  -d '{"question": "Compare the 2024 and 2025 return policies: what changed for opened items?"}'
```

Swagger UI: http://localhost:5210/swagger

## Benchmark

```bash
curl -X POST http://localhost:5210/benchmark/run
```

Runs the 6 questions in `data/benchmark-questions.json` (simple, multi-hop,
tricky, out-of-domain) through both pipelines and stores the report in
MongoDB with per-run latency, token counts, LLM calls and tool calls.
`GET /benchmark/latest` returns the most recent report.

Every single run (naive or agentic) is also logged to the `runs` collection,
including the full tool trace of agentic runs.

## Using OpenAI instead of Ollama

Everything goes through the `IChatClient` / `IEmbeddingGenerator`
abstractions, so switching provider is configuration only:

```jsonc
// appsettings.json
"AI": {
  "Provider": "openai",
  "OpenAI": {
    "ApiKey": "sk-...",
    "ChatModel": "gpt-4o-mini",
    "EmbeddingModel": "text-embedding-3-small"
  }
}
```

Note: `text-embedding-3-small` has 1536 dimensions — set
`Qdrant:VectorSize` accordingly and re-seed.

## How retrieval works

Hybrid search on Qdrant with two named vectors per chunk:

- **dense** — embeddings from the configured embedding model, cosine distance;
- **sparse** — a minimal BM25-style encoding (token hashing + log-scaled term
  frequency, IDF applied server-side by Qdrant via `Modifier.Idf`).

Both are queried in a single `Query` API call and fused server-side with
Reciprocal Rank Fusion.

## Project layout

```
src/RagVsAgenticRag.Api/
├── Program.cs                     # slim host: Serilog, Swagger, endpoint mapping
├── Endpoints/                     # MapXEndpoints extension classes
├── Filters/ValidationFilter.cs    # DataAnnotations validation for Minimal API
├── Extensions/                    # DI wiring (AI clients, Qdrant, Mongo)
├── Services/
│   ├── ChunkingService.cs         # heading-aligned markdown chunking
│   ├── SparseEncoder.cs           # BM25-style sparse vectors
│   ├── VectorSearchService.cs     # Qdrant hybrid search (RRF fusion)
│   ├── IngestionService.cs        # seed pipeline
│   ├── NaiveRagService.cs         # the whole naive pipeline (~30 lines of logic)
│   ├── AgenticRagService.cs       # agent loop via UseFunctionInvocation
│   ├── AgentToolContext.cs        # the 3 tools + per-run trace
│   └── RunLogService.cs           # MongoDB persistence
└── Models/                        # typed DTOs (records)
data/docs/                         # fictional company dataset (Nordwind Outdoor)
data/benchmark-questions.json      # the 6 benchmark questions
```

## What the agent trace looks like

Serilog logs every tool call of an agentic run:

```
[10:42:01 INF] AGENT step 1 | search_docs(query="return policy 2024" topK=5) -> 5 chunks (5 new) (38 ms)
[10:42:02 INF] AGENT step 2 | search_docs(query="return policy 2025" topK=5) -> 5 chunks (4 new) (35 ms)
[10:42:05 INF] AGENT step 3 | rerank_evaluate(question="Compare..." chunks=9) -> SUFFICIENT: both policies retrieved (2810 ms)
```

That trace is also returned in the API response (`trace`) and persisted in
MongoDB, so you can inspect exactly what the agent did on every run.

## License

MIT
