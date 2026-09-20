using System.IO;

namespace TextReaderMM.Core;

/// <summary>
/// Downloaded and generated documents are written to disk first, so the rest of the
/// application can treat every source as an ordinary file.
/// </summary>
public static class TempFiles
{
    public static string CreateTempFilePath(string prefix)
    {
        var folder = Path.Combine(Path.GetTempPath(), "TextReaderMM");
        Directory.CreateDirectory(folder);

        return Path.Combine(folder, $"{prefix}_{Guid.NewGuid():N}.txt");
    }

    public static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // The file may still be open; it lives in the temp folder anyway.
        }
    }
}
