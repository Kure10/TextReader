using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TextReaderMM.Core;
using TextReaderMM.Core.Interfaces;

namespace TextReaderMM.Controls;

/// <summary>
/// Puts the <see cref="TextView"/> together with its scrollbars and keeps the two in sync.
/// The vertical scrollbar works in (fractional) line units, so dragging it scrolls smoothly
/// even across billions of lines.
/// </summary>
public partial class TextViewer : UserControl
{
    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(ITextDocument), typeof(TextViewer), new PropertyMetadata(null, OnDocumentChanged));

    public static readonly DependencyProperty LineCountProperty = DependencyProperty.Register(
        nameof(LineCount), typeof(long), typeof(TextViewer), new PropertyMetadata(0L));

    public static readonly DependencyProperty FirstVisibleLineProperty = DependencyProperty.Register(
        nameof(FirstVisibleLine), typeof(long), typeof(TextViewer),
        new FrameworkPropertyMetadata(0L, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty SearchTermProperty = DependencyProperty.Register(
        nameof(SearchTerm), typeof(string), typeof(TextViewer), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MatchCaseProperty = DependencyProperty.Register(
        nameof(MatchCase), typeof(bool), typeof(TextViewer), new PropertyMetadata(false));

    public static readonly DependencyProperty CurrentMatchProperty = DependencyProperty.Register(
        nameof(CurrentMatch), typeof(SearchMatch?), typeof(TextViewer), new PropertyMetadata(null));

    // Guards the scrollbar -> view -> scrollbar feedback loop.
    private bool _isSyncing;

    public TextViewer()
    {
        InitializeComponent();

        View.ScrollChanged += (_, _) => SyncScrollBars();

        // The text view owns the keyboard navigation, so it must hold the focus.
        Loaded += (_, _) => View.Focus();
        VerticalScrollBar.Scroll += OnVerticalScroll;
        HorizontalScrollBar.Scroll += OnHorizontalScroll;
    }

    public ITextDocument? Document
    {
        get => (ITextDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public long LineCount
    {
        get => (long)GetValue(LineCountProperty);
        set => SetValue(LineCountProperty, value);
    }

    public long FirstVisibleLine
    {
        get => (long)GetValue(FirstVisibleLineProperty);
        set => SetValue(FirstVisibleLineProperty, value);
    }

    public string SearchTerm
    {
        get => (string)GetValue(SearchTermProperty);
        set => SetValue(SearchTermProperty, value);
    }

    public bool MatchCase
    {
        get => (bool)GetValue(MatchCaseProperty);
        set => SetValue(MatchCaseProperty, value);
    }

    public SearchMatch? CurrentMatch
    {
        get => (SearchMatch?)GetValue(CurrentMatchProperty);
        set => SetValue(CurrentMatchProperty, value);
    }

    /// <summary>Hands the keyboard back to the text, e.g. after the search bar is closed.</summary>
    public void FocusText() => View.Focus();

    /// <summary>Glides to the given line and puts the keyboard back into the text.</summary>
    public void ScrollToLine(long line)
    {
        View.AnimateTo(line);
        View.Focus();
    }

    /// <summary>After a file is opened the focus sits in the menu, so it is handed back.</summary>
    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TextViewer)d).View.Focus();

    private void OnVerticalScroll(object sender, ScrollEventArgs e)
    {
        if (_isSyncing)
            return;

        View.VerticalScrollPosition = VerticalScrollBar.Value;
    }

    private void OnHorizontalScroll(object sender, ScrollEventArgs e)
    {
        if (_isSyncing)
            return;

        View.HorizontalOffset = HorizontalScrollBar.Value;
    }

    private void SyncScrollBars()
    {
        _isSyncing = true;

        try
        {
            var viewportLines = View.ViewportLineCount;

            VerticalScrollBar.ViewportSize = viewportLines;
            VerticalScrollBar.Maximum = View.MaxVerticalScrollPosition;
            VerticalScrollBar.SmallChange = 1;
            VerticalScrollBar.LargeChange = Math.Max(1, viewportLines - 1);
            VerticalScrollBar.Value = View.VerticalScrollPosition;

            // The document width is unknown, so the widest line currently drawn is used.
            var maxWidth = Math.Max(0, View.WidestVisibleLineWidth - View.ActualWidth);

            HorizontalScrollBar.ViewportSize = View.ActualWidth;
            HorizontalScrollBar.Maximum = maxWidth;
            HorizontalScrollBar.SmallChange = 16;
            HorizontalScrollBar.LargeChange = Math.Max(16, View.ActualWidth - 32);
            HorizontalScrollBar.Value = Math.Min(View.HorizontalOffset, maxWidth);
        }
        finally
        {
            _isSyncing = false;
        }
    }
}
