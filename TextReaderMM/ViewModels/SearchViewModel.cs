using System.Windows.Input;
using TextReaderMM.Core;
using TextReaderMM.Core.Interfaces;

namespace TextReaderMM.ViewModels;

/// <summary>
/// State of the search bar. Searching runs on a background thread so that a miss
/// in a multi-gigabyte file does not freeze the window.
/// </summary>
public sealed class SearchViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    private ITextDocument? _document;
    private string _searchText = string.Empty;
    private bool _isVisible;
    private bool _matchCase;
    private bool _isSearching;
    private string _resultText = string.Empty;
    private SearchMatch? _currentMatch;

    public SearchViewModel()
    {
        FindNextCommand = new RelayCommand(() => Find(forward: true), CanSearch);
        FindPreviousCommand = new RelayCommand(() => Find(forward: false), CanSearch);
        CloseCommand = new RelayCommand(() => IsVisible = false);
    }

    public ICommand FindNextCommand { get; }
    public ICommand FindPreviousCommand { get; }
    public ICommand CloseCommand { get; }

    public ITextDocument? Document
    {
        get => _document;
        set
        {
            if (!SetField(ref _document, value))
                return;

            CurrentMatch = null;
            ResultText = string.Empty;
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value))
                return;

            // A new term means the next F3 starts from the top of the viewport again.
            CurrentMatch = null;
            ResultText = string.Empty;
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    public bool MatchCase
    {
        get => _matchCase;
        set => SetField(ref _matchCase, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set => SetField(ref _isSearching, value);
    }

    public string ResultText
    {
        get => _resultText;
        private set => SetField(ref _resultText, value);
    }

    /// <summary>The match the view highlights and scrolls to.</summary>
    public SearchMatch? CurrentMatch
    {
        get => _currentMatch;
        private set => SetField(ref _currentMatch, value);
    }

    /// <summary>
    /// First visible line, kept up to date by the view. A search with no previous
    /// match starts here rather than at the beginning of the document.
    /// </summary>
    public long FirstVisibleLine { get; set; }

    private bool CanSearch() => Document is not null && !string.IsNullOrEmpty(SearchText) && !IsSearching;

    private async void Find(bool forward)
    {
        var document = Document;
        if (document is null || string.IsNullOrEmpty(SearchText))
            return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var term = SearchText;
        var matchCase = MatchCase;

        var (startLine, startColumn) = GetSearchOrigin(forward);

        IsSearching = true;
        ResultText = "searching...";

        try
        {
            var match = await Task.Run(() => forward
                    ? TextSearcher.FindNext(document, term, startLine, startColumn, matchCase, ct)
                    : TextSearcher.FindPrevious(document, term, startLine, startColumn, matchCase, ct),
                ct);

            if (ct.IsCancellationRequested)
                return;

            CurrentMatch = match;
            ResultText = match is null ? "not found" : $"line {match.Value.LineIndex + 1:N0}";
        }
        catch (OperationCanceledException)
        {
            // A newer search took over.
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                IsSearching = false;
        }
    }

    private (long Line, int Column) GetSearchOrigin(bool forward)
    {
        if (CurrentMatch is not { } match)
            return (FirstVisibleLine, 0);

        // Continue right after (or before) the current match, so F3 keeps moving.
        return forward
            ? (match.LineIndex, match.ColumnIndex + Math.Max(1, match.Length))
            : (match.LineIndex, match.ColumnIndex);
    }
}
