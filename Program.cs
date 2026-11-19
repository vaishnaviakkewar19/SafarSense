using System.Text;
using Microsoft.Extensions.AI;
using OllamaSharp;
using SafarSense;

// ---------------------------------------------------------------------------
// Program.cs is the ONLY file that knows we're using Ollama.
// Swap these two clients for Azure OpenAI / OpenAI and RagEngine doesn't change.
// ---------------------------------------------------------------------------

const int TopK = 3;
const string OllamaEndpoint = "http://localhost:11434";
// Tip: switch EmbeddingModel to "bge-m3" (run `ollama pull bge-m3`) for multilingual embeddings,
// which lets you ask questions in Hindi, e.g. "मुझे पहाड़ और झीलें चाहिए".
const string EmbeddingModel = "all-minilm";
const string ChatModel = "llama3.2";

// Vector "x-ray" for learning: set ShowVectors = false for a clean demo.
const bool ShowVectors = true;
const int PreviewDims = 8;    // numbers printed per vector (raise to 384 to see all of them)
const int SparklineDims = 48; // dims drawn as a mini bar chart

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// Ctrl+C cancels in-flight AI calls and exits cleanly instead of killing the process.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
var ct = cts.Token;

ConsoleUi.Banner();

// OllamaApiClient implements both IEmbeddingGenerator and IChatClient.
using var embeddingClient = new OllamaApiClient(new Uri(OllamaEndpoint), EmbeddingModel);
using var chatClient = new OllamaApiClient(new Uri(OllamaEndpoint), ChatModel);

var rag = new RagEngine(embeddingClient, chatClient);

try
{
    ConsoleUi.WriteLine($"Embedding knowledge base with '{EmbeddingModel}'...", ConsoleColor.DarkGray);
    await rag.IndexAsync(KnowledgeBase.Destinations, ct);
    ConsoleUi.Info($"✔ Indexed {rag.Count} destinations ({rag.Dimensions}-dimensional vectors)");
    Console.WriteLine();

    if (ShowVectors)
    {
        VectorView.PrintKnowledgeBase(rag.Entries, PreviewDims, SparklineDims);
    }
}
catch (HttpRequestException)
{
    ConsoleUi.Error($"✖ Could not reach Ollama at {OllamaEndpoint}.");
    ConsoleUi.WriteLine("  1. Start Ollama (open the Ollama app or run `ollama serve`)", ConsoleColor.Yellow);
    ConsoleUi.WriteLine($"  2. ollama pull {EmbeddingModel}", ConsoleColor.Yellow);
    ConsoleUi.WriteLine($"  3. ollama pull {ChatModel}", ConsoleColor.Yellow);
    return 1;
}
catch (OperationCanceledException)
{
    return 0;
}

try
{
    while (!ct.IsCancellationRequested)
    {
        ConsoleUi.Write("Where do you want to go? (or 'exit') › ", ConsoleColor.Cyan);
        var query = Console.ReadLine()?.Trim();

        if (query is null || query.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        if (query.Length == 0)
        {
            continue;
        }

        // Retrieve: embed the query once, then rank sentences by similarity to it.
        var queryVector = await rag.EmbedAsync(query, ct);
        if (ShowVectors)
        {
            VectorView.PrintQuery(queryVector.Span, PreviewDims, SparklineDims);
        }

        var hits = rag.Search(queryVector, TopK);

        Console.WriteLine();
        ConsoleUi.Heading($"Top {TopK} matches:");
        for (var i = 0; i < hits.Count; i++)
        {
            ConsoleUi.Write($"  [{i + 1}] ", ConsoleColor.Yellow);
            ConsoleUi.Write($"{hits[i].Score:F3}  ", ConsoleColor.Green);
            Console.WriteLine(hits[i].Text);
        }

        if (ShowVectors)
        {
            VectorView.PrintInsights(queryVector.Span, hits, rag.Entries);
        }

        // Generate: stream a grounded answer token by token.
        Console.WriteLine();
        ConsoleUi.Heading("SafarSense suggests:");
        Console.Write("  ");
        await foreach (var update in rag.SummarizeAsync(query, hits, ct))
        {
            ConsoleUi.Write(update.Text, ConsoleColor.White);
        }

        Console.WriteLine();
        Console.WriteLine();
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine();
}

ConsoleUi.Info("Shubh yatra! Happy travels. 🙏");
return 0;
