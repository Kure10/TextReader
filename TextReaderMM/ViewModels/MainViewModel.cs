using System.IO;
using System.Windows.Input;
using TextReaderMM.Core;
using TextReaderMM.Core.Interfaces;
using TextReaderMM.ViewModels.Interfaces;

namespace TextReaderMM.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;

    private ITextDocument? _document;
    private long _lineCount;
    private bool _isBusy;
    private string _statusText = "No file loaded";

    public MainViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        OpenFileCommand = new RelayCommand(OpenFile, () => !IsBusy);
    }

    public ICommand OpenFileCommand { get; }

    /// <summary>Everything around the search bar lives in its own view model.</summary>
    public SearchViewModel Search { get; } = new();

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

    /// <summary>Kept separate from the document because it grows while indexing runs.</summary>
    public long LineCount
    {
        get => _lineCount;
        private set => SetField(ref _lineCount, value);
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

    private void OpenFile()
    {
        var path = _dialogs.ShowOpenFileDialog();
        if (path is null)
            return;

        IsBusy = true;

        try
        {
            // Progress<T> marshals the callback back to the UI thread for us.
            var progress = new Progress<IndexingProgress>(OnIndexingProgress);
            var newDocument = IndexedTextDocument.Open(path, progress);

            Document?.Dispose();
            Document = newDocument;
            Search.Document = newDocument;
            LineCount = 0;
            StatusText = $"{FormatSize(newDocument.FileSize)}  |  {newDocument.Encoding}  |  indexing...";
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

    private void OnIndexingProgress(IndexingProgress progress)
    {
        if (Document is null)
            return;

        LineCount = progress.LineCount;

        StatusText = progress.IsComplete
            ? $"{FormatSize(Document.FileSize)}  |  {Document.Encoding}  |  {progress.LineCount:N0} lines"
            : $"{FormatSize(Document.FileSize)}  |  {Document.Encoding}  |  {progress.LineCount:N0} lines  |  indexing {progress.Ratio:P0}";
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
