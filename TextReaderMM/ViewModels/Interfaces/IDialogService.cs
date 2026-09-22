namespace TextReaderMM.ViewModels.Interfaces;

/// <summary>Keeps WPF dialogs out of view models.</summary>
public interface IDialogService
{
    string? ShowOpenFileDialog();

    string? ShowSaveFileDialog(string defaultFileName);

    /// <summary>Returns the address to download, or null when cancelled.</summary>
    string? ShowUrlDialog();

    /// <summary>Returns how many lines to generate, or null when cancelled.</summary>
    long? ShowGenerateTextDialog();

    /// <summary>Returns the zero-based line to jump to, or null when cancelled.</summary>
    long? ShowGoToLineDialog(long lineCount, long currentLine);

    void ShowShortcuts();

    /// <summary>Opens a progress window; cancelling it cancels the given token source.</summary>
    IProgressDialog ShowProgress(string title, CancellationTokenSource cts);

    void ShowError(string message);
}
