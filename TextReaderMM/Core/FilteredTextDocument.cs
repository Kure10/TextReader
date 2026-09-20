using TextReaderMM.Core.Interfaces;

namespace TextReaderMM.Core;

/// <summary>
/// Shows only the lines whose numbers are in the given list. It does not copy any text:
/// reads are forwarded to the underlying document, so the filter costs 8 bytes per matching line.
/// The source document stays owned by whoever created it and is not disposed here.
/// </summary>
public sealed class FilteredTextDocument(ITextDocument source, IReadOnlyList<long> matchingLines) : ITextDocument
{
    public string FilePath => source.FilePath;
    public long FileSize => source.FileSize;
    public TextEncodingKind Encoding => source.Encoding;
    public long LineCount => matchingLines.Count;
    public bool IsIndexingComplete => source.IsIndexingComplete;

    public string GetLine(long index)
    {
        if (index < 0 || index >= matchingLines.Count)
            return string.Empty;

        return source.GetLine(matchingLines[(int)index]);
    }

    /// <summary>Line number in the original document, used for display.</summary>
    public long GetSourceLine(long index)
        => index < 0 || index >= matchingLines.Count ? -1 : matchingLines[(int)index];

    public void Dispose()
    {
        // The underlying document is shared and outlives the filter.
    }
}
