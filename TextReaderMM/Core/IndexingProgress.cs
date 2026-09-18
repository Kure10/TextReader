namespace TextReaderMM.Core;

/// <summary>Snapshot of the indexing state, reported to the UI while scanning.</summary>
public readonly record struct IndexingProgress(long LineCount, long BytesProcessed, long TotalBytes, bool IsComplete)
{
    public double Ratio => TotalBytes <= 0 ? 1 : (double)BytesProcessed / TotalBytes;
}
