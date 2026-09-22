using System.Windows;
using Microsoft.Win32;
using TextReaderMM.ViewModels.Interfaces;
using TextReaderMM.Views;

namespace TextReaderMM;

public sealed class DialogService(Window owner) : IDialogService
{
    private const string Filter = "Text files (*.txt;*.log;*.csv)|*.txt;*.log;*.csv|All files (*.*)|*.*";

    public string? ShowOpenFileDialog()
    {
        var dialog = new OpenFileDialog { Filter = Filter };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string defaultFileName)
    {
        var dialog = new SaveFileDialog { Filter = Filter, FileName = defaultFileName };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public string? ShowUrlDialog()
    {
        var window = new UrlInputWindow { Owner = owner };
        return window.ShowDialog() == true ? window.Url : null;
    }

    public long? ShowGenerateTextDialog()
    {
        var window = new GenerateTextWindow { Owner = owner };
        return window.ShowDialog() == true ? window.LineCount : null;
    }

    public long? ShowGoToLineDialog(long lineCount, long currentLine)
    {
        var window = new GoToLineWindow(lineCount, currentLine) { Owner = owner };
        return window.ShowDialog() == true ? window.LineIndex : null;
    }

    public void ShowShortcuts()
    {
        var window = new ShortcutsWindow { Owner = owner };
        window.ShowDialog();
    }

    public IProgressDialog ShowProgress(string title, CancellationTokenSource cts)
    {
        var window = new ProgressWindow(title, cts) { Owner = owner };
        window.Show();

        return window;
    }

    public bool Confirm(string title, string message)
        => MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ShowError(string message)
        => MessageBox.Show(owner, message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
}
