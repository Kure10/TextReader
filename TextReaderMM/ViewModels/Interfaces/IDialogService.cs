namespace TextReaderMM.ViewModels.Interfaces;

/// <summary>Keeps WPF dialogs out of view models.</summary>
public interface IDialogService
{
    string? ShowOpenFileDialog();
    string? ShowSaveFileDialog(string defaultFileName);
    void ShowError(string message);
}
