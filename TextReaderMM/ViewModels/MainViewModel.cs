using System.IO;
using System.Windows.Input;
using TextReaderMM.Core;

namespace TextReaderMM.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;

    private ITextDocument? _document;
    private IReadOnlyList<string> _visibleLines = [];
    private bool _isBusy;
    private string _statusText = "No file loaded";

    public MainViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        OpenFileCommand = new RelayCommand(OpenFile, () => !IsBusy);
    }

    public ICommand OpenFileCommand { get; }

    public ITextDocument? Document
    {
        get => _document;
        private set
        {
            if (!SetField(ref _document, value))
                return;

            OnPropertyChanged(nameof(Title));
        }
    }

    /// <summary>
    /// TEMPORARY: lines bound to a ListBox until the custom virtualized TextView exists.
    /// </summary>
    public IReadOnlyList<string> VisibleLines
    {
        get => _visibleLines;
        private set => SetField(ref _visibleLines, value);
    }
    
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string Title => Document is null
        ? "TextReaderMM"
        : $"{Path.GetFileName(Document.FilePath)} - TextReaderMM";

    private async void OpenFile()
    {
        string? path = _dialogs.ShowOpenFileDialog();
        if (path is null)
            return;

        IsBusy = true;
        StatusText = $"Loading {Path.GetFileName(path)}...";

        try
        {
            var newDocument = await InMemoryTextDocument.LoadAsync(path);

            Document?.Dispose();
            Document = newDocument;
            VisibleLines = Enumerable.Range(0, (int)newDocument.LineCount)
                .Select(i => newDocument.GetLine(i))
                .ToList();

            StatusText = $"{FormatSize(newDocument.FileSize)}  |  {newDocument.LineCount:N0} lines";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Failed to open file:\n{ex.Message}");
            StatusText = "Load failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.##} {units[unit]}";
    }
}
