using System.IO;

namespace TextReaderMM.Core;

/// <summary>
/// Copies a file in blocks so the progress can be reported and the work cancelled.
/// File.Copy would be slightly faster but gives neither.
/// </summary>
public static class FileCopier
{
    private const int BufferSize = 4 * 1024 * 1024;

    /// <summary>Progress reports the ratio of bytes copied, from 0 to 1.</summary>
    public static async Task CopyAsync(string sourcePath, string targetPath, IProgress<double>? progress, CancellationToken ct)
    {
        await using var source = new FileStream(
            sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);

        await using var target = new FileStream(
            targetPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

        var total = source.Length;
        var buffer = new byte[BufferSize];
        long copied = 0;

        try
        {
            while (true)
            {
                var read = await source.ReadAsync(buffer, ct);

                if (read == 0)
                    break;

                await target.WriteAsync(buffer.AsMemory(0, read), ct);

                copied += read;
                progress?.Report(total <= 0 ? 1 : (double)copied / total);
            }
        }
        catch (OperationCanceledException)
        {
            // Half a file is worse than none, so the partial copy is removed.
            target.Close();
            TempFiles.TryDelete(targetPath);
            throw;
        }
    }

    /// <summary>Free space on the volume the path points to, or null when it cannot be determined.</summary>
    public static long? GetFreeSpace(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));

            if (string.IsNullOrEmpty(root))
                return null;

            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception)
        {
            // Network shares and unusual paths may not report free space at all.
            return null;
        }
    }
}
