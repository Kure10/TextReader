using System.Windows;
using Microsoft.Win32;
using TextReaderMM.ViewModels.Interfaces;

namespace TextReaderMM;

public sealed class DialogService(Window owner) : IDialogService
{
    private const string Filter = "Text files (*.txt;*.log;*.csv)|*.txt;*.log;*.csv|All files (*.*)|*.*";

    public string? ShowOpenFileDialog()
    {
        OpenFileDialog dialog = new OpenFileDialog { Filter = Filter };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string defaultFileName)
    {
        SaveFileDialog dialog = new SaveFileDialog { Filter = Filter, FileName = defaultFileName };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public void ShowError(string message)
        => MessageBox.Show(owner, message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
}
