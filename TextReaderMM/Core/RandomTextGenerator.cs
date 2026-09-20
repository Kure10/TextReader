using System.IO;
using System.Text;

namespace TextReaderMM.Core;

/// <summary>
/// Writes a file of randomly generated lines of very different lengths, which is what
/// the assignment asks for as one of the data sources.
/// </summary>
public static class RandomTextGenerator
{
    private const int BufferSize = 1024 * 1024;
    private const int LongLineChance = 40;   // roughly every 40th line is a long one
    private const int EmptyLineChance = 50;

    private static readonly string[] Words =
    [
        "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta", "iota", "kappa",
        "lambda", "sigma", "omega", "error", "warning", "info", "debug", "trace", "user", "session",
        "request", "response", "payload", "database", "cache", "queue", "thread", "socket", "buffer",
        "index", "offset", "encoding", "stream", "file", "line", "token", "handle", "cluster", "shard"
    ];

    /// <summary>Returns the path of the generated file; progress reports written lines.</summary>
    public static async Task<string> GenerateToTempFileAsync(long lineCount, IProgress<long>? progress, CancellationToken ct)
    {
        var path = TempFiles.CreateTempFilePath("random");
        var random = new Random();

        await using var writer = new StreamWriter(path, false, new UTF8Encoding(false), BufferSize);
        var builder = new StringBuilder(1024);

        for (long line = 1; line <= lineCount; line++)
        {
            ct.ThrowIfCancellationRequested();

            builder.Clear();
            builder.Append(line.ToString("D9")).Append(' ');

            if (random.Next(EmptyLineChance) == 0)
            {
                // An empty line now and then, so the reader has to cope with those too.
                builder.Clear();
            }
            else
            {
                var words = random.Next(LongLineChance) == 0
                    ? random.Next(400, 1500)
                    : random.Next(1, 20);

                for (var i = 0; i < words; i++)
                    builder.Append(Words[random.Next(Words.Length)]).Append(' ');
            }

            await writer.WriteLineAsync(builder, ct);

            if (line % 50_000 == 0)
                progress?.Report(line);
        }

        progress?.Report(lineCount);
        return path;
    }
}
