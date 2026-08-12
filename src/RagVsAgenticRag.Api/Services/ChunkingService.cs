namespace RagVsAgenticRag.Api.Services;

public record Chunk(string Source, int Index, string Text);

public class ChunkingService
{
    private const int MaxChunkChars = 900;

    /// <summary>
    /// Splits a markdown document into heading-aligned chunks, prefixing each
    /// chunk with the document title so retrieval keeps its context.
    /// </summary>
    public IReadOnlyList<Chunk> ChunkMarkdown(string source, string markdown)
    {
        var title = markdown
            .Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("# "))?[2..] ?? source;

        return SplitByHeadings(markdown)
            .SelectMany(SplitLongSection)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Select((text, index) => new Chunk(source, index, $"[{title}]\n{text}"))
            .ToList();
    }

    private static IEnumerable<string> SplitByHeadings(string markdown)
    {
        var current = new List<string>();
        foreach (var line in markdown.Split('\n'))
        {
            if (line.StartsWith('#') && current.Count > 0)
            {
                yield return string.Join('\n', current);
                current.Clear();
            }

            current.Add(line);
        }

        if (current.Count > 0)
            yield return string.Join('\n', current);
    }

    private static IEnumerable<string> SplitLongSection(string section)
    {
        if (section.Length <= MaxChunkChars)
        {
            yield return section;
            yield break;
        }

        var current = new List<string>();
        var currentLength = 0;
        foreach (var paragraph in section.Split("\n\n"))
        {
            if (currentLength + paragraph.Length > MaxChunkChars && current.Count > 0)
            {
                yield return string.Join("\n\n", current);
                current.Clear();
                currentLength = 0;
            }

            current.Add(paragraph);
            currentLength += paragraph.Length + 2;
        }

        if (current.Count > 0)
            yield return string.Join("\n\n", current);
    }
}
