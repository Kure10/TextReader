using System.IO;
using System.Windows.Input;
using TextReaderMM.Core;
using TextReaderMM.Core.Interfaces;
using TextReaderMM.ViewModels.Interfaces;

namespace TextReaderMM.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IDialogService _dialogs;

    private ITextDocument? _document;
    private string? _tempFilePath;
    private long _lineCount;
    private bool _isBusy;
    private string _statusText = "No file loaded";

    public MainViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;

        OpenFileCommand = new RelayCommand(OpenFile, () => !IsBusy);
        OpenUrlCommand = new RelayCommand(OpenUrl, () => !IsBusy);
        GenerateRandomTextCommand = new RelayCommand(GenerateRandomText, () => !IsBusy);
        SaveAsCommand = new RelayCommand(SaveAs, () => !IsBusy && Document is not null);
    }

    public ICommand OpenFileCommand { get; }
    public ICommand OpenUrlCommand { get; }
    public ICommand GenerateRandomTextCommand { get; }
    public ICommand SaveAsCommand { get; }

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

        OpenDocument(path, isTemporary: false);
    }

    private async void OpenUrl()
    {
        var url = _dialogs.ShowUrlDialog();
        if (string.IsNullOrWhiteSpace(url))
            return;

        IsBusy = true;
        StatusText = $"Downloading {url}...";

        try
        {
            var progress = new Progress<long>(bytes => StatusText = $"Downloading {url}  |  {FormatSize(bytes)}");
            var path = await WebTextDownloader.DownloadToTempFileAsync(url, progress, CancellationToken.None);

            OpenDocument(path, isTemporary: true);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Download failed:\n{ex.Message}");
            StatusText = "Download failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void GenerateRandomText()
    {
        var lineCount = _dialogs.ShowGenerateTextDialog();
        if (lineCount is null)
            return;

        IsBusy = true;
        StatusText = "Generating...";

        try
        {
            var progress = new Progress<long>(written => StatusText = $"Generating  |  {written:N0} / {lineCount:N0} lines");
            var path = await RandomTextGenerator.GenerateToTempFileAsync(lineCount.Value, progress, CancellationToken.None);

            OpenDocument(path, isTemporary: true);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Generating failed:\n{ex.Message}");
            StatusText = "Generating failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void SaveAs()
    {
        var document = Document;
        if (document is null)
            return;

        var target = _dialogs.ShowSaveFileDialog(Path.GetFileName(document.FilePath));
        if (target is null)
            return;

        IsBusy = true;
        StatusText = "Saving...";

        try
        {
            var source = document.FilePath;

            // The document is just a file on disk, so saving is a plain copy
            // and never loads the content into memory.
            await Task.Run(() => File.Copy(source, target, overwrite: true));

            StatusText = $"Saved to {target}";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"Saving failed:\n{ex.Message}");
            StatusText = "Saving failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Single entry point for every source: a picked file, a download or generated text.
    /// </summary>
    private void OpenDocument(string path, bool isTemporary)
    {
        try
        {
            var progress = new Progress<IndexingProgress>(OnIndexingProgress);
            var newDocument = IndexedTextDocument.Open(path, progress);

            Document?.Dispose();
            TempFiles.TryDelete(_tempFilePath);

            _tempFilePath = isTemporary ? path : null;
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

    public void Dispose()
    {
        Document?.Dispose();
        TempFiles.TryDelete(_tempFilePath);
    }
}
