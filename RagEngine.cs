using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace SafarSense;

/// <summary>A single retrieval result: the matched sentence and how similar it is to the query.</summary>
/// <param name="Text">The knowledge-base sentence.</param>
/// <param name="Score">Cosine similarity in [-1, 1]; higher means closer in meaning.</param>
public sealed record SearchHit(string Text, float Score);

/// <summary>One knowledge-base sentence together with its embedding vector.</summary>
public sealed record IndexedSentence(string Text, ReadOnlyMemory<float> Vector);

/// <summary>
/// A minimal Retrieval-Augmented Generation engine.
/// It depends only on the Microsoft.Extensions.AI abstractions, so any provider
/// (Ollama, Azure OpenAI, OpenAI, ...) can be plugged in without changing this class.
/// </summary>
/// <param name="embeddingGenerator">Turns text into vectors that capture meaning.</param>
/// <param name="chatClient">The LLM used to write the final, grounded answer.</param>
public sealed class RagEngine(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IChatClient chatClient)
{
    private const string SystemPrompt =
        "You are SafarSense, a friendly Indian travel guide. Answer using ONLY the numbered context sentences. " +
        "Give a warm 2-4 sentence recommendation and cite sources like [1], [2]. " +
        "If the context doesn't answer the question, say: I don't have that information yet.";

    private readonly List<IndexedSentence> _index = [];

    /// <summary>The indexed sentences and their vectors (read-only), handy for inspecting what the model produced.</summary>
    public IReadOnlyList<IndexedSentence> Entries => _index;

    /// <summary>Number of sentences currently indexed.</summary>
    public int Count => _index.Count;

    /// <summary>Length of each embedding vector (e.g. 384 for all-minilm, 1024 for bge-m3).</summary>
    public int Dimensions => _index.Count > 0 ? _index[0].Vector.Length : 0;

    /// <summary>
    /// Step 1 — Indexing. Converts every sentence into an embedding vector and stores it in memory.
    /// We send all sentences in ONE batched call because a round trip per sentence is much slower.
    /// </summary>
    public async Task IndexAsync(IEnumerable<string> sentences, CancellationToken cancellationToken = default)
    {
        // GenerateAndZipAsync pairs each input string with its embedding, so we never mix up the order.
        var pairs = await embeddingGenerator.GenerateAndZipAsync(sentences, cancellationToken: cancellationToken);

        _index.Clear();
        foreach (var (text, embedding) in pairs)
        {
            _index.Add(new IndexedSentence(text, embedding.Vector));
        }
    }

    /// <summary>
    /// Step 2 — Retrieval. Embeds the query using the SAME model as the index (vectors from different
    /// models aren't comparable), then ranks sentences by cosine similarity, which measures how
    /// closely two vectors point in the same direction, regardless of their length.
    /// </summary>
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, CancellationToken cancellationToken = default)
    {
        var queryVector = await EmbedAsync(query, cancellationToken);
        return Search(queryVector, topK);
    }

    /// <summary>Embeds a single piece of text with the same model used for the index.</summary>
    public async Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
        await embeddingGenerator.GenerateVectorAsync(text, cancellationToken: cancellationToken);

    /// <summary>
    /// Ranks indexed sentences against an already-embedded query. Split out from <see cref="SearchAsync"/>
    /// so callers can inspect the query vector without paying for a second embedding call.
    /// </summary>
    public IReadOnlyList<SearchHit> Search(ReadOnlyMemory<float> queryVector, int topK)
    {
        // Brute-force scoring is fine for 10 items; a vector database replaces this at scale.
        return _index
            .Select(item => new SearchHit(item.Text, TensorPrimitives.CosineSimilarity(queryVector.Span, item.Vector.Span)))
            .OrderByDescending(hit => hit.Score)
            .Take(topK)
            .ToList();
    }

    /// <summary>
    /// Step 3 — Augmented generation. Puts the retrieved sentences into the prompt as numbered context
    /// and asks the LLM to answer from that context only. This "grounding" reduces hallucination and
    /// lets the model cite where each fact came from. Streaming shows tokens as soon as they're produced.
    /// </summary>
    public async IAsyncEnumerable<ChatResponseUpdate> SummarizeAsync(
        string query,
        IReadOnlyList<SearchHit> hits,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var context = string.Join(Environment.NewLine, hits.Select((hit, i) => $"[{i + 1}] {hit.Text}"));

        List<ChatMessage> messages =
        [
            new(ChatRole.System, SystemPrompt),
            // Repeating the key rule right after the question helps small models (like 3B llama3.2) obey it.
            new(ChatRole.User,
                $"""
                Context:
                {context}

                Question: {query}

                Reply in one short paragraph (no lists). Use only facts from the context above.
                If the context doesn't answer the question, reply exactly: I don't have that information yet.
                """),
        ];

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, cancellationToken: cancellationToken))
        {
            yield return update;
        }
    }
}
