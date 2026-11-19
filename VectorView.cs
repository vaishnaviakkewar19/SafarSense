using System.Numerics.Tensors;

namespace SafarSense;

/// <summary>
/// Console visualisations that make embedding vectors less abstract.
/// A vector is just an array of floats; each position ("dimension") is one learned feature of meaning.
/// </summary>
public static class VectorView
{
    private const string Bars = "▁▂▃▄▅▆▇█";
    private const int Indent = 24;

    /// <summary>Prints a one-glance preview of every indexed sentence's vector.</summary>
    public static void PrintKnowledgeBase(IReadOnlyList<IndexedSentence> entries, int previewDims, int sparkDims)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var dims = entries[0].Vector.Length;
        ConsoleUi.Heading($"Knowledge base vectors (first {Math.Min(previewDims, dims)} of {dims} dimensions):");
        for (var i = 0; i < entries.Count; i++)
        {
            ConsoleUi.Write($"  [{i + 1,2}] ", ConsoleColor.Yellow);
            ConsoleUi.Write(Label(entries[i].Text).PadRight(Indent - 7), ConsoleColor.White);
            PrintVector(entries[i].Vector.Span, previewDims, sparkDims);
        }

        PrintLegend(sparkDims);
        Console.WriteLine();
    }

    /// <summary>Prints the query's vector in the same format as the knowledge base, so you can compare by eye.</summary>
    public static void PrintQuery(ReadOnlySpan<float> vector, int previewDims, int sparkDims)
    {
        Console.WriteLine();
        ConsoleUi.Heading("Query vector:");
        ConsoleUi.Write("  [ Q] ", ConsoleColor.Yellow);
        ConsoleUi.Write("your query".PadRight(Indent - 7), ConsoleColor.White);
        PrintVector(vector, previewDims, sparkDims);
    }

    /// <summary>
    /// Explains WHERE the similarity score comes from. Cosine similarity = dot(q, d) / (‖q‖·‖d‖),
    /// and the dot product is the sum of q[i]·d[i] over every dimension, so we can see which
    /// dimensions push the score up and which pull it down.
    /// </summary>
    public static void PrintInsights(ReadOnlySpan<float> query, IReadOnlyList<SearchHit> hits, IReadOnlyList<IndexedSentence> entries, int topDims = 5)
    {
        if (hits.Count == 0)
        {
            return;
        }

        var best = entries.First(e => e.Text == hits[0].Text).Vector.Span;
        var dims = query.Length;

        // Per-dimension contribution to the dot product.
        var contributions = new float[dims];
        TensorPrimitives.Multiply(query, best, contributions);
        var dot = TensorPrimitives.Sum(contributions);
        var queryNorm = TensorPrimitives.Norm(query);
        var bestNorm = TensorPrimitives.Norm(best);

        var ranked = Enumerable.Range(0, dims).OrderByDescending(i => contributions[i]).ToArray();
        var pushUp = ranked.Take(topDims).ToArray();
        var pullDown = ranked.Reverse().Take(topDims).ToArray();
        var positiveDims = contributions.Count(c => c > 0);

        Console.WriteLine();
        ConsoleUi.Heading($"Vector insights (query vs [1] {Label(hits[0].Text)}):");

        Bullet($"Length ‖q‖ = {queryNorm:F3}, ‖d‖ = {bestNorm:F3}. " +
               (Math.Abs(queryNorm - 1) < 0.01 && Math.Abs(bestNorm - 1) < 0.01
                   ? "Both are unit vectors, so cosine similarity is just the dot product."
                   : "Cosine divides the dot product by these lengths, so only direction matters."));

        Bullet($"Dot product = Σ q[i]·d[i] over {dims} dims = {dot:F3} → cosine = {hits[0].Score:F3}.");

        ConsoleUi.Write("  • Pushing the score UP:   ", ConsoleColor.Gray);
        ConsoleUi.WriteLine(string.Join("  ", pushUp.Select(i => $"d{i} {contributions[i]:+0.000;-0.000}")), ConsoleColor.Green);
        ConsoleUi.Write("  • Pulling the score DOWN: ", ConsoleColor.Gray);
        ConsoleUi.WriteLine(string.Join("  ", pullDown.Select(i => $"d{i} {contributions[i]:+0.000;-0.000}")), ConsoleColor.Red);

        var topShare = pushUp.Sum(i => contributions[i]);
        Bullet($"The top {topDims} dims add {topShare:+0.000} and {positiveDims}/{dims} dims agree in sign. " +
               "Meaning is spread across many dimensions; no single one stands for a concept like \"beach\".");

        if (hits.Count > 1)
        {
            Bullet($"Gap between #1 and #2: {hits[0].Score - hits[1].Score:F3}. " +
                   (hits[0].Score - hits[1].Score < 0.02 ? "Very close, so the ranking is fragile." : "A clear winner."));
        }
    }

    private static void PrintVector(ReadOnlySpan<float> vector, int previewDims, int sparkDims)
    {
        var shown = Math.Min(previewDims, vector.Length);
        var values = new string[shown];
        for (var i = 0; i < shown; i++)
        {
            values[i] = vector[i].ToString("+0.000;-0.000; 0.000");
        }

        ConsoleUi.Write($"‖v‖={TensorPrimitives.Norm(vector):F3}  ", ConsoleColor.DarkGray);
        Console.Write($"[{string.Join(" ", values)}");
        ConsoleUi.WriteLine(vector.Length > shown ? $" … +{vector.Length - shown} more]" : "]", ConsoleColor.DarkGray);

        Console.Write(new string(' ', Indent));
        PrintSparkline(vector[..Math.Min(sparkDims, vector.Length)]);
    }

    /// <summary>Bar height = magnitude, colour = sign. Similar sentences show similar patterns.</summary>
    private static void PrintSparkline(ReadOnlySpan<float> values)
    {
        // MaxMagnitude returns the original (possibly negative) value, so take Abs.
        var maxAbs = MathF.Abs(TensorPrimitives.MaxMagnitude(values));
        if (maxAbs == 0)
        {
            maxAbs = 1;
        }

        foreach (var v in values)
        {
            var level = (int)MathF.Round(MathF.Abs(v) / maxAbs * (Bars.Length - 1));
            ConsoleUi.Write(Bars[level].ToString(), v >= 0 ? ConsoleColor.Cyan : ConsoleColor.Red);
        }

        Console.WriteLine();
    }

    private static void PrintLegend(int sparkDims) =>
        ConsoleUi.WriteLine($"{new string(' ', Indent)}↑ first {sparkDims} dims · bar height = magnitude · cyan = positive, red = negative",
            ConsoleColor.DarkGray);

    private static void Bullet(string text) => ConsoleUi.WriteLine($"  • {text}", ConsoleColor.Gray);

    /// <summary>Short destination name from a sentence, e.g. "Kerala's backwaters…" → "Kerala".</summary>
    private static string Label(string sentence)
    {
        var text = sentence.StartsWith("The ", StringComparison.Ordinal) ? sentence[4..] : sentence;
        string[] separators = ["'s ", ",", " is ", " has ", " on ", " in ", " are "];
        var end = separators
            .Select(s => text.IndexOf(s, StringComparison.Ordinal))
            .Where(i => i > 0)
            .DefaultIfEmpty(Math.Min(text.Length, 14))
            .Min();
        return text[..end];
    }
}
