using System.IO;
using System.Windows.Input;
using TextReaderMM.Core;

namespace TextReaderMM.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;

    private ITextDocument? _document;
    private VirtualLineList? _lines;
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

    /// <summary>TEMPORARY: lines shown in the placeholder ListBox.</summary>
    public VirtualLineList? Lines
    {
        get => _lines;
        private set => SetField(ref _lines, value);
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
            Lines = null;
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

        // Rebuilding the list is what makes newly indexed lines visible in the ListBox.
        Lines = new VirtualLineList(Document, progress.LineCount);

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
