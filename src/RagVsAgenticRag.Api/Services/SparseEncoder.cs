using System.Text.RegularExpressions;

namespace RagVsAgenticRag.Api.Services;

/// <summary>
/// Minimal BM25-style sparse encoder: tokenize, drop stopwords, hash each
/// term to a stable index, weight by log-scaled term frequency. IDF weighting
/// is applied server-side by Qdrant (Modifier.Idf on the sparse vector).
/// </summary>
public partial class SparseEncoder
{
    private static readonly HashSet<string> Stopwords =
    [
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "has",
        "have", "how", "in", "is", "it", "its", "of", "on", "or", "that", "the",
        "this", "to", "was", "were", "what", "when", "which", "will", "with"
    ];

    public (uint[] Indices, float[] Values) Encode(string text)
    {
        var weighted = TokenRegex()
            .Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => t.Length > 1 && !Stopwords.Contains(t))
            .GroupBy(Fnv1aHash)
            .ToDictionary(g => g.Key, g => (float)(1 + Math.Log(g.Count())));

        return (weighted.Keys.ToArray(), weighted.Values.ToArray());
    }

    private static uint Fnv1aHash(string token) =>
        token.Aggregate(2166136261u, (hash, c) => (hash ^ c) * 16777619u);

    [GeneratedRegex(@"[a-z0-9]+")]
    private static partial Regex TokenRegex();
}
