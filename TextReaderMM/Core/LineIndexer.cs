using System.Diagnostics;
using System.IO;

namespace TextReaderMM.Core;

/// <summary>
/// Streams the whole file once and records line offsets into a <see cref="LineIndex"/>.
/// Nothing is decoded and no text is kept, so memory use stays flat regardless of file size.
/// </summary>
public static class LineIndexer
{
    private const int BufferSize = 1024 * 1024;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(200);

    public static void Build(
        string filePath,
        int preambleLength,
        ILineBreakScanner scanner,
        LineIndex index,
        IProgress<IndexingProgress>? progress,
        CancellationToken ct)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            BufferSize,
            FileOptions.SequentialScan);

        var totalBytes = stream.Length;
        var buffer = new byte[BufferSize];
        var stopwatch = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;

        long filePosition = preambleLength;
        long lineCount = 0;
        long currentLineStart = preambleLength;

        // Block 0 starts at the first line of the file.
        index.AddBlockStart(currentLineStart);
        stream.Position = filePosition;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;

            var bufferDataOffset = filePosition - preambleLength;
            var searchFrom = 0;

            while (true)
            {
                var breakIndex = scanner.FindLineBreak(buffer.AsSpan(0, read), searchFrom, bufferDataOffset);
                if (breakIndex < 0)
                    break;

                lineCount++;
                currentLineStart = filePosition + breakIndex + scanner.BytesToNextLine;

                if (lineCount % LineIndex.LinesPerBlock == 0)
                    index.AddBlockStart(currentLineStart);

                searchFrom = breakIndex + 1;
            }

            filePosition += read;
            index.ReportProgress(lineCount, currentLineStart);

            if (progress is not null && stopwatch.Elapsed - lastReport >= ProgressInterval)
            {
                lastReport = stopwatch.Elapsed;
                progress.Report(new IndexingProgress(lineCount, filePosition, totalBytes, IsComplete: false));
            }
        }

        // A file not ending with a line break still has one last, unterminated line.
        if (currentLineStart < totalBytes)
            lineCount++;

        index.Complete(lineCount, totalBytes);
        progress?.Report(new IndexingProgress(lineCount, totalBytes, totalBytes, IsComplete: true));
    }
}
