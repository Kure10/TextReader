namespace TextReaderMM.Core;

/// <summary>
/// Sparse line index: instead of one offset per line it stores the byte offset
/// of every <see cref="LinesPerBlock"/>-th line. For a 50 GB file this costs
/// single-digit megabytes, while a full index would need gigabytes.
/// Written by the indexing thread, read by the UI thread, hence the lock.
/// </summary>
public sealed class LineIndex
{
    public const int LinesPerBlock = 1000;

    private readonly List<long> _blockStarts = [];
    private readonly Lock _sync = new();

    private long _lineCount;
    private long _scannedUpTo;
    private bool _isComplete;

    /// <summary>Number of lines known so far; grows while indexing runs.</summary>
    public long LineCount
    {
        get
        {
            lock (_sync)
                return _lineCount;
        }
    }

    public bool IsComplete
    {
        get
        {
            lock (_sync)
                return _isComplete;
        }
    }

    /// <summary>Records the file offset of the first line of the next block.</summary>
    public void AddBlockStart(long fileOffset)
    {
        lock (_sync)
            _blockStarts.Add(fileOffset);
    }

    /// <summary>Publishes how far the indexer got, so the UI can already show it.</summary>
    public void ReportProgress(long lineCount, long scannedUpTo)
    {
        lock (_sync)
        {
            _lineCount = lineCount;
            _scannedUpTo = scannedUpTo;
        }
    }

    public void Complete(long lineCount, long endOffset)
    {
        lock (_sync)
        {
            _lineCount = lineCount;
            _scannedUpTo = endOffset;
            _isComplete = true;
        }
    }

    /// <summary>
    /// Byte range of the given block: from the first line of the block up to the
    /// first line of the next one (or to the end of the indexed area).
    /// </summary>
    public (long Start, long End) GetBlockRange(int blockIndex)
    {
        lock (_sync)
        {
            var start = _blockStarts[blockIndex];
            var end = blockIndex + 1 < _blockStarts.Count ? _blockStarts[blockIndex + 1] : _scannedUpTo;
            return (start, end);
        }
    }
}
