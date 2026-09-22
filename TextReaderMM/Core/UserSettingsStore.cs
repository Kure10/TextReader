using System.IO;
using System.Text.Json;
using TextReaderMM.Diagnostics;

namespace TextReaderMM.Core;

/// <summary>
/// Reads and writes the settings file. It lives in the user's AppData folder, because
/// the folder next to the executable is usually not writable.
/// A missing or broken file is never fatal; the defaults are used instead.
/// </summary>
public static class UserSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TextReaderMM",
        "settings.json");

    public static UserSettings? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json, Options);

            Log.Info($"Settings loaded from {FilePath}");
            return settings;
        }
        catch (Exception exception)
        {
            // A hand-edited or truncated file must not stop the application from starting.
            Log.Warning($"Could not read {FilePath}, using defaults: {exception.Message}");
            return null;
        }
    }

    public static void Save(UserSettings settings)
    {
        try
        {
            var folder = Path.GetDirectoryName(FilePath);

            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
            Log.Info($"Settings saved to {FilePath}");
        }
        catch (Exception exception)
        {
            Log.Warning($"Could not write {FilePath}: {exception.Message}");
        }
    }
}
