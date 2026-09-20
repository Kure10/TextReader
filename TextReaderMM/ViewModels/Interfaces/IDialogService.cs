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

    void ShowError(string message);
}
