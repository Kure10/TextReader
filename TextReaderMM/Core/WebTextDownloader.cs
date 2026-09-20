using System.IO;
using System.Net.Http;

namespace TextReaderMM.Core;

/// <summary>
/// Downloads a URL straight to a temporary file. The response is streamed, so even a
/// huge document never has to fit in memory, and the result is just another text file.
/// </summary>
public static class WebTextDownloader
{
    private const int BufferSize = 128 * 1024;

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    /// <summary>Returns the path of the downloaded file; progress reports bytes received.</summary>
    public static async Task<string> DownloadToTempFileAsync(string url, IProgress<long>? progress, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Enter a valid http or https address.", nameof(url));

        var path = TempFiles.CreateTempFilePath("web");

        using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, BufferSize, useAsync: true);

        var buffer = new byte[BufferSize];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, ct);
            if (read == 0)
                break;

            await target.WriteAsync(buffer.AsMemory(0, read), ct);

            total += read;
            progress?.Report(total);
        }

        return path;
    }
}
