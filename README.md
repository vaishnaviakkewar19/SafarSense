# SafarSense 🧭 — an AI travel buddy for India

A small console app that shows **Retrieval-Augmented Generation (RAG)** in .NET 10 using
**Microsoft.Extensions.AI** with a local **Ollama** model.

## What is RAG?

An LLM only knows what it was trained on, and it can make things up confidently. RAG fixes this by
first **retrieving** the facts that matter from *your* data using semantic (meaning-based) search, then
handing those facts to the LLM and asking it to answer **only** from them. You get answers grounded in
your own content, with citations, and no retraining.

## How it works

```
  10 destination sentences
            │
            ▼  IEmbeddingGenerator (all-minilm) — one batched call
  10 embedding vectors (384-d)  ◄── stored in memory
            │
  user query ──► query vector
            │
            ▼  TensorPrimitives.CosineSimilarity (query vs every sentence)
  scores ──► sort ──► top-k (3) sentences
            │
            ▼  IChatClient (llama3.2) — "answer ONLY from this numbered context"
  streamed answer with [1] [2] citations
```

| File | Role |
|---|---|
| `KnowledgeBase.cs` | The 10 sentences we search over |
| `RagEngine.cs` | Index → Search → Summarize. Depends **only** on MEAI interfaces |
| `ConsoleUi.cs` | Coloured console helper |
| `Program.cs` | The **only** file that knows about Ollama; wires everything up |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.com) running locally at `http://localhost:11434`
- Models:
  ```
  ollama pull all-minilm
  ollama pull llama3.2
  ```

## Run

```
dotnet run
```

Type a question at the prompt. Type `exit` (or press Ctrl+C) to quit.

## Demo questions

| Ask | Expected top match |
|---|---|
| Snowy mountains and high-altitude lakes | Ladakh |
| Romantic boat trip | Kerala |
| I love history and old ruins | Hampi (Jaipur / Varanasi nearby) |
| Spiritual place with river rituals | Varanasi |
| Underwater adventure | Andaman |
| Best biryani in Hyderabad? | *"I don't have that information yet."* (grounding) |

> **Teaching moment:** ask `I want snow mountains and lakes`. With the small `all-minilm` model,
> **Rann of Kutch** edges out Ladakh (0.252 vs 0.249) because "snow" sits close to "white … winter"
> in its vector space. Small models capture rough meaning, not world knowledge. Better models,
> richer source text (e.g. "snow-capped Himalayan peaks") or hybrid keyword + vector search fix this.

### Hindi queries

`all-minilm` is English-only. In `Program.cs` set `EmbeddingModel = "bge-m3"` (`ollama pull bge-m3`)
for multilingual embeddings, then try `मुझे पहाड़ और झीलें चाहिए`. The vector size changes to 1024.

## Swap to Azure OpenAI (only Program.cs changes)

```
dotnet add package Azure.AI.OpenAI
dotnet add package Microsoft.Extensions.AI.OpenAI
dotnet add package Azure.Identity
```

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;

var azure = new AzureOpenAIClient(new Uri("https://<your-resource>.openai.azure.com"), new DefaultAzureCredential());

IEmbeddingGenerator<string, Embedding<float>> embeddingClient =
    azure.GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator();
IChatClient chatClient =
    azure.GetChatClient("gpt-4o-mini").AsIChatClient();

var rag = new RagEngine(embeddingClient, chatClient);   // RagEngine is untouched
```

`RagEngine` has no idea which provider it's talking to. That's the point of Microsoft.Extensions.AI.

## Roadmap

1. **Microsoft.Extensions.VectorData**: replace the in-memory list with a real vector store (Qdrant, Azure AI Search, Postgres/pgvector, ...).
2. **Real document chunking**: ingest PDFs and web pages and split them into overlapping chunks.
3. **ASP.NET Core Minimal API**: expose `/search` and `/ask` endpoints (stream with SSE).
4. **Blazor UI**: a chat front-end with live streaming and clickable citations.
5. **Microsoft Agent Framework**: an itinerary-planning agent with tools (weather, trains, budget).
