using System.IO;

namespace TextReaderMM.Core;

/// <summary>
/// TEMPORARY implementation for step 1: loads all lines into memory.
/// Only suitable for small files; will be replaced by an indexed, disk-backed document.
/// </summary>
public sealed class InMemoryTextDocument : ITextDocument
{
    private readonly List<string> _lines;

    private InMemoryTextDocument(string filePath, long fileSize, List<string> lines)
    {
        FilePath = filePath;
        FileSize = fileSize;
        _lines = lines;
    }

    public string FilePath { get; }
    public long FileSize { get; }
    public long LineCount => _lines.Count;
    public bool IsIndexingComplete => true;

    // Never raised by this implementation; kept to satisfy the interface.
    public event EventHandler<double>? IndexingProgress { add { } remove { } }

    public string GetLine(long index) => _lines[(int)index];

    public static async Task<InMemoryTextDocument> LoadAsync(string filePath, CancellationToken ct = default)
    {
        var info = new FileInfo(filePath);
        var lines = new List<string>();

        using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
        while (await reader.ReadLineAsync(ct) is { } line)
            lines.Add(line);

        return new InMemoryTextDocument(filePath, info.Length, lines);
    }

    public void Dispose() => _lines.Clear();
}
