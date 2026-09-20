using System.Diagnostics;

namespace TextReaderMM.Diagnostics;

/// <summary>
/// Developer-facing logging. Unlike IDialogService, which talks to the user, this writes
/// diagnostics for us: stack traces, background failures and things the user must not be
/// bothered with. It is static on purpose — it may be called from anywhere, including Core,
/// and must never change how the program behaves.
/// Messages go to the process output, which the IDE shows in its Console tab, and to the
/// trace listeners, which is the Debug Output tab. Running the application outside an IDE
/// has nowhere to write them, which is harmless.
/// </summary>
public static class Log
{
    /// <summary>Ordinary progress messages; compiled out of Release builds entirely.</summary>
    [Conditional("DEBUG")]
    public static void Info(string message) => Write("INFO", message);

    /// <summary>Something unexpected that the application recovered from.</summary>
    public static void Warning(string message) => Write("WARN", message);

    /// <summary>A failure; the exception is logged with its stack trace.</summary>
    public static void Error(string message, Exception? exception = null)
    {
        Write("ERROR", message, isError: true);

        if (exception is not null)
            Write("ERROR", exception.ToString(), isError: true);
    }

    private static void Write(string level, string message, bool isError = false)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";

        // Errors go to the error stream, which the IDE console highlights.
        if (isError)
            Console.Error.WriteLine(line);
        else
            Console.Out.WriteLine(line);

        Trace.WriteLine(line);
    }
}
