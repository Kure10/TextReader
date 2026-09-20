using TextReaderMM.Core.Interfaces;

namespace TextReaderMM.Core;

/// <summary>
/// Finds occurrences of a term in a document. Only one match is looked up at a time,
/// starting from the current position, so a hit near the caret is found immediately
/// even in a file with millions of matches.
/// Reads go through <see cref="ITextDocument"/>, which reads lines block by block,
/// so a full scan costs one disk read per 1000 lines.
/// </summary>
public static class TextSearcher
{
    /// <summary>
    /// First match at or after the given position. When the end is reached the search
    /// wraps around to the beginning, which is what F3 in an editor normally does.
    /// </summary>
    public static SearchMatch? FindNext(
        ITextDocument document,
        string term,
        long startLine,
        int startColumn,
        bool matchCase,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(term))
            return null;

        var comparison = GetComparison(matchCase);
        var lineCount = document.LineCount;

        for (long line = Math.Max(0, startLine); line < lineCount; line++)
        {
            ct.ThrowIfCancellationRequested();

            var from = line == startLine ? startColumn : 0;
            var match = FindInLine(document, term, line, from, comparison);

            if (match is not null)
                return match;
        }

        // Wrap around: everything before the starting line, including it.
        for (long line = 0; line <= Math.Min(startLine, lineCount - 1); line++)
        {
            ct.ThrowIfCancellationRequested();

            var match = FindInLine(document, term, line, 0, comparison);

            if (match is not null)
                return match;
        }

        return null;
    }

    /// <summary>Last match before the given position, wrapping to the end of the document.</summary>
    public static SearchMatch? FindPrevious(
        ITextDocument document,
        string term,
        long startLine,
        int startColumn,
        bool matchCase,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(term))
            return null;

        var comparison = GetComparison(matchCase);
        var lineCount = document.LineCount;

        for (var line = Math.Min(startLine, lineCount - 1); line >= 0; line--)
        {
            ct.ThrowIfCancellationRequested();

            var before = line == startLine ? startColumn : int.MaxValue;
            var match = FindInLineBackwards(document, term, line, before, comparison);

            if (match is not null)
                return match;
        }

        for (var line = lineCount - 1; line > startLine; line--)
        {
            ct.ThrowIfCancellationRequested();

            var match = FindInLineBackwards(document, term, line, int.MaxValue, comparison);

            if (match is not null)
                return match;
        }

        return null;
    }

    /// <summary>
    /// Line numbers of all lines containing the term, used by the optional filter.
    /// The result is capped so that a term matching everything cannot exhaust memory.
    /// </summary>
    public static List<long> FindMatchingLines(
        ITextDocument document,
        string term,
        bool matchCase,
        int maxResults,
        IProgress<long>? progress,
        CancellationToken ct)
    {
        var comparison = GetComparison(matchCase);
        var result = new List<long>();
        var lineCount = document.LineCount;

        for (long line = 0; line < lineCount && result.Count < maxResults; line++)
        {
            ct.ThrowIfCancellationRequested();

            if (document.GetLine(line).Contains(term, comparison))
                result.Add(line);

            if (line % 100_000 == 0)
                progress?.Report(line);
        }

        return result;
    }

    private static SearchMatch? FindInLine(ITextDocument document, string term, long line, int from, StringComparison comparison)
    {
        var text = document.GetLine(line);

        if (from >= text.Length)
            return null;

        var index = text.IndexOf(term, Math.Max(0, from), comparison);
        return index < 0 ? null : new SearchMatch(line, index, term.Length);
    }

    private static SearchMatch? FindInLineBackwards(ITextDocument document, string term, long line, int before, StringComparison comparison)
    {
        var text = document.GetLine(line);

        if (text.Length == 0)
            return null;

        // LastIndexOf searches backwards from the given index, so it has to stay in range.
        var startAt = Math.Min(before - 1, text.Length - 1);

        if (startAt < 0)
            return null;

        var index = text.LastIndexOf(term, startAt, comparison);
        return index < 0 ? null : new SearchMatch(line, index, term.Length);
    }

    private static StringComparison GetComparison(bool matchCase)
        => matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
}
