using System.IO;
using System.Text;
using TextReaderMM.Core.Interfaces;
using TextReaderMM.Diagnostics;

namespace TextReaderMM.Core;

/// <summary>
/// Disk backed document. Only the sparse index lives in memory; line text is read
/// on demand and cached per block, so the memory footprint does not grow with file size.
/// </summary>
public sealed class IndexedTextDocument : ITextDocument
{
    /// <summary>Longest line the reader will materialize; the rest is cut off for display.</summary>
    private const int MaxLineBytes = 8 * 1024;

    private const int ReadBufferSize = 256 * 1024;

    private readonly FileStream _stream;
    private readonly LineIndex _index = new();
    private readonly ILineBreakScanner _scanner;
    private readonly Encoding _encoding;
    private readonly int _preambleLength;
    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _readSync = new();

    // Two most recently used blocks are kept decoded; scrolling stays inside them.
    private CachedBlock? _currentBlock;
    private CachedBlock? _previousBlock;

    private IndexedTextDocument(string filePath, FileStream stream, TextEncodingKind encoding, int preambleLength)
    {
        FilePath = filePath;
        FileSize = stream.Length;
        Encoding = encoding;

        _stream = stream;
        _preambleLength = preambleLength;
        _scanner = LineBreakScannerFactory.Create(encoding);
        _encoding = TextEncodingDetector.ToEncoding(encoding);
    }

    public string FilePath { get; }
    public long FileSize { get; }
    public TextEncodingKind Encoding { get; }
    public long LineCount => _index.LineCount;
    public bool IsIndexingComplete => _index.IsComplete;

    /// <summary>Runs until the whole file is indexed; the document is usable right away.</summary>
    public Task IndexingTask { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Opens the file, detects its encoding and starts indexing in the background.
    /// </summary>
    public static IndexedTextDocument Open(string filePath, IProgress<IndexingProgress>? progress)
    {
        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            ReadBufferSize,
            FileOptions.RandomAccess);

        TextEncodingKind encoding = TextEncodingDetector.Detect(stream, out var preambleLength);
        IndexedTextDocument document = new IndexedTextDocument(filePath, stream, encoding, preambleLength);

        Log.Info($"Opening {filePath} ({stream.Length:N0} bytes, {encoding}, BOM {preambleLength} B)");

        document.IndexingTask = Task
            .Run(() => LineIndexer.Build(filePath, preambleLength, document._scanner, document._index, progress, document._cts.Token),
                document._cts.Token)
            .ContinueWith(task => LogIndexingResult(task, filePath), TaskScheduler.Default);

        return document;
    }

    /// <summary>
    /// Indexing runs on a background thread, so without this its exception would be
    /// swallowed and the file would just look shorter than it is.
    /// </summary>
    private static void LogIndexingResult(Task task, string filePath)
    {
        if (task.IsFaulted)
            Log.Error($"Indexing of {filePath} failed", task.Exception?.GetBaseException());
        else if (task.IsCanceled)
            Log.Info($"Indexing of {filePath} was cancelled");
        else
            Log.Info($"Indexing of {filePath} finished");
    }

    public string GetLine(long index)
    {
        if (index < 0 || index >= LineCount)
            return string.Empty;

        var blockIndex = (int)(index / LineIndex.LinesPerBlock);
        var lineInBlock = (int)(index % LineIndex.LinesPerBlock);
        var block = GetBlock(blockIndex);

        return lineInBlock < block.Lines.Count ? block.Lines[lineInBlock] : string.Empty;
    }

    private CachedBlock GetBlock(int blockIndex)
    {
        if (_currentBlock?.BlockIndex == blockIndex)
            return _currentBlock;

        if (_previousBlock?.BlockIndex == blockIndex)
        {
            (_currentBlock, _previousBlock) = (_previousBlock, _currentBlock);
            return _currentBlock!;
        }

        var block = LoadBlock(blockIndex);
        _previousBlock = _currentBlock;
        _currentBlock = block;
        return block;
    }

    /// <summary>
    /// Reads one block (up to 1000 lines) from disk and decodes it. The file is read
    /// sequentially in chunks and every line longer than <see cref="MaxLineBytes"/> is cut off,
    /// so a single gigantic line cannot blow up memory.
    /// </summary>
    private CachedBlock LoadBlock(int blockIndex)
    {
        var (start, end) = _index.GetBlockRange(blockIndex);
        var lines = new List<string>(LineIndex.LinesPerBlock);

        var buffer = new byte[ReadBufferSize];
        var lineBuffer = new byte[MaxLineBytes];
        var lineLength = 0;
        var lineTruncated = false;
        var position = start;

        lock (_readSync)
        {
            _stream.Position = start;

            while (position < end && lines.Count < LineIndex.LinesPerBlock)
            {
                var toRead = (int)Math.Min(buffer.Length, end - position);
                var read = _stream.Read(buffer, 0, toRead);
                if (read == 0)
                    break;

                var bufferDataOffset = position - _preambleLength;
                var consumed = 0;

                while (consumed < read && lines.Count < LineIndex.LinesPerBlock)
                {
                    var breakIndex = _scanner.FindLineBreak(buffer.AsSpan(0, read), consumed, bufferDataOffset);
                    var contentEnd = breakIndex < 0 ? read : breakIndex;

                    AppendToLine(buffer.AsSpan(consumed, contentEnd - consumed), lineBuffer, ref lineLength, ref lineTruncated);

                    if (breakIndex < 0)
                    {
                        consumed = read;
                        break;
                    }

                    lines.Add(DecodeLine(lineBuffer, lineLength, lineTruncated));
                    lineLength = 0;
                    lineTruncated = false;
                    consumed = breakIndex + _scanner.BytesToNextLine;
                }

                position += read;
            }
        }

        // Trailing line of the file without a line break.
        if (lines.Count < LineIndex.LinesPerBlock && lineLength > 0)
            lines.Add(DecodeLine(lineBuffer, lineLength, lineTruncated));

        return new CachedBlock(blockIndex, lines);
    }

    private static void AppendToLine(ReadOnlySpan<byte> source, byte[] lineBuffer, ref int lineLength, ref bool truncated)
    {
        if (source.IsEmpty)
            return;

        var free = lineBuffer.Length - lineLength;
        if (free <= 0)
        {
            truncated = true;
            return;
        }

        if (source.Length > free)
        {
            source = source[..free];
            truncated = true;
        }

        source.CopyTo(lineBuffer.AsSpan(lineLength));
        lineLength += source.Length;
    }

    private string DecodeLine(byte[] lineBuffer, int length, bool truncated)
    {
        // UTF-16 always uses byte pairs, so an odd tail byte would decode to garbage.
        if (Encoding != TextEncodingKind.Utf8)
            length -= length % 2;

        var text = _encoding.GetString(lineBuffer, 0, length).TrimEnd('\r');
        return truncated ? text + " ..." : text;
    }

    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            IndexingTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Cancellation is expected here.
        }

        _cts.Dispose();
        _stream.Dispose();
        _currentBlock = null;
        _previousBlock = null;
    }

    private sealed record CachedBlock(int BlockIndex, List<string> Lines);
}
