using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TextReaderMM.Core;
using TextReaderMM.Core.Interfaces;

namespace TextReaderMM.Controls;

/// <summary>
/// Draws only the lines that are currently visible, straight into the drawing context.
/// No per-line visuals exist, so the memory cost does not depend on the document size.
/// The scroll position is kept as "first visible line + pixel offset inside that line",
/// which is what makes smooth (sub-line) scrolling possible.
/// </summary>
public sealed class TextView : FrameworkElement
{
    private const double WheelLinesPerNotch = 3;
    private const double HorizontalStep = 48;

    /// <summary>Higher value means a shorter, snappier glide.</summary>
    private const double AnimationSpeed = 14;

    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(ITextDocument), typeof(TextView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDocumentChanged));

    /// <summary>
    /// Bound separately from the document because it keeps growing while indexing runs.
    /// </summary>
    public static readonly DependencyProperty LineCountProperty = DependencyProperty.Register(
        nameof(LineCount), typeof(long), typeof(TextView),
        new FrameworkPropertyMetadata(0L, FrameworkPropertyMetadataOptions.AffectsRender, OnLineCountChanged));

    /// <summary>Reports the top line back to the view model, which starts searches there.</summary>
    public static readonly DependencyProperty FirstVisibleLineProperty = DependencyProperty.Register(
        nameof(FirstVisibleLine), typeof(long), typeof(TextView),
        new FrameworkPropertyMetadata(0L, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty SearchTermProperty = DependencyProperty.Register(
        nameof(SearchTerm), typeof(string), typeof(TextView),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MatchCaseProperty = DependencyProperty.Register(
        nameof(MatchCase), typeof(bool), typeof(TextView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentMatchProperty = DependencyProperty.Register(
        nameof(CurrentMatch), typeof(SearchMatch?), typeof(TextView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnCurrentMatchChanged));

    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register(
        nameof(FontFamily), typeof(FontFamily), typeof(TextView),
        new FrameworkPropertyMetadata(new FontFamily("Consolas"), FrameworkPropertyMetadataOptions.AffectsRender, OnFontChanged));

    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
        nameof(FontSize), typeof(double), typeof(TextView),
        new FrameworkPropertyMetadata(14.0, FrameworkPropertyMetadataOptions.AffectsRender, OnFontChanged));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(TextView),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background), typeof(Brush), typeof(TextView),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush MatchBrush = CreateFrozenBrush(Color.FromRgb(255, 233, 150));
    private static readonly Brush CurrentMatchBrush = CreateFrozenBrush(Color.FromRgb(255, 165, 60));

    private Typeface _typeface = new("Consolas");
    private double _lineHeight = 16;
    private double _pixelsPerDip = 1;

    private long _firstVisibleLine;
    private double _lineOffset;
    private double _horizontalOffset;
    private double _widestVisibleLine;

    // Smooth scrolling: the view chases a target position instead of jumping to it.
    private double _targetPosition;
    private bool _isAnimating;
    private TimeSpan _lastFrameTime;

    public TextView()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    /// <summary>Raised whenever the visible range changes, so scrollbars can follow.</summary>
    public event EventHandler? ScrollChanged;

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

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Brush Background
    {
        get => (Brush)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public double LineHeight => _lineHeight;

    /// <summary>How many lines fit into the viewport, fractions included.</summary>
    public double ViewportLineCount => _lineHeight <= 0 ? 0 : ActualHeight / _lineHeight;

    /// <summary>
    /// Scroll position measured in lines; the fractional part is the offset inside
    /// the first visible line. Setting it is how the scrollbar drives the view.
    /// </summary>
    public double VerticalScrollPosition
    {
        get => _firstVisibleLine + (_lineHeight <= 0 ? 0 : _lineOffset / _lineHeight);
        set
        {
            StopAnimation();
            SetVerticalScrollPosition(value);
        }
    }

    public double HorizontalOffset
    {
        get => _horizontalOffset;
        set
        {
            var clamped = Math.Max(0, value);
            if (Math.Abs(clamped - _horizontalOffset) < 0.01)
                return;

            _horizontalOffset = clamped;
            InvalidateVisual();
        }
    }

    /// <summary>Width of the widest line drawn in the last render pass.</summary>
    public double WidestVisibleLineWidth => _widestVisibleLine;

    public double MaxVerticalScrollPosition => Math.Max(0, LineCount - ViewportLineCount);

    public void ScrollByLines(double deltaLines) => VerticalScrollPosition += deltaLines;

    public void ScrollToLine(long line) => VerticalScrollPosition = line;

    /// <summary>
    /// Eases towards the given position over the next few frames. Used by the mouse wheel
    /// and by keyboard navigation, so even a jump to the end of the document glides.
    /// </summary>
    public void AnimateTo(double position)
    {
        _targetPosition = Math.Clamp(position, 0, MaxVerticalScrollPosition);

        if (_isAnimating)
            return;

        _isAnimating = true;
        _lastFrameTime = TimeSpan.Zero;
        CompositionTarget.Rendering += OnAnimationFrame;
    }

    public void AnimateByLines(double deltaLines)
        => AnimateTo((_isAnimating ? _targetPosition : VerticalScrollPosition) + deltaLines);

    /// <summary>Stops the animation; dragging the scrollbar must win over it.</summary>
    public void StopAnimation()
    {
        if (!_isAnimating)
            return;

        _isAnimating = false;
        CompositionTarget.Rendering -= OnAnimationFrame;
    }

    /// <summary>Brings a line into view, keeping it in the upper third of the viewport.</summary>
    public void EnsureLineVisible(long line)
    {
        var viewport = ViewportLineCount;
        var first = VerticalScrollPosition;

        if (line >= first && line < first + viewport - 1)
            return;

        AnimateTo(line - viewport / 3);
    }

    private void OnAnimationFrame(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args)
            return;

        var elapsed = _lastFrameTime == TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(16)
            : args.RenderingTime - _lastFrameTime;

        _lastFrameTime = args.RenderingTime;

        var current = VerticalScrollPosition;
        var remaining = _targetPosition - current;

        if (Math.Abs(remaining) < 0.02)
        {
            StopAnimation();
            SetVerticalScrollPosition(_targetPosition);
            return;
        }

        // Exponential easing: fast at first, slowing down near the target,
        // and independent of the frame rate.
        var factor = 1 - Math.Exp(-AnimationSpeed * elapsed.TotalSeconds);
        SetVerticalScrollPosition(current + remaining * factor);
    }

    private void SetVerticalScrollPosition(double position)
    {
        var clamped = Math.Clamp(position, 0, MaxVerticalScrollPosition);
        var line = (long)Math.Floor(clamped);
        var offset = (clamped - line) * _lineHeight;

        if (line == _firstVisibleLine && Math.Abs(offset - _lineOffset) < 0.01)
            return;

        _firstVisibleLine = line;
        _lineOffset = offset;
        FirstVisibleLine = line;

        InvalidateVisual();
        ScrollChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var page = Math.Max(1, ViewportLineCount - 1);

        switch (e.Key)
        {
            case Key.Down:
                AnimateByLines(1);
                break;
            case Key.Up:
                AnimateByLines(-1);
                break;
            case Key.PageDown:
                AnimateByLines(page);
                break;
            case Key.PageUp:
                AnimateByLines(-page);
                break;
            case Key.Home:
                AnimateTo(0);
                break;
            case Key.End:
                AnimateTo(MaxVerticalScrollPosition);
                break;
            case Key.Left:
                HorizontalOffset -= HorizontalStep;
                break;
            case Key.Right:
                HorizontalOffset += HorizontalStep;
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawRectangle(Background, null, new Rect(RenderSize));

        var document = Document;
        if (document is null || LineCount == 0 || _lineHeight <= 0)
            return;

        _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        _widestVisibleLine = 0;

        var y = -_lineOffset;
        var lineIndex = _firstVisibleLine;

        while (y < ActualHeight && lineIndex < LineCount)
        {
            var text = document.GetLine(lineIndex);
            var formatted = CreateFormattedText(text);
            var origin = new Point(-_horizontalOffset, y);

            _widestVisibleLine = Math.Max(_widestVisibleLine, formatted.WidthIncludingTrailingWhitespace);

            DrawSearchHighlights(drawingContext, formatted, text, lineIndex, origin);
            drawingContext.DrawText(formatted, origin);

            y += _lineHeight;
            lineIndex++;
        }
    }

    /// <summary>
    /// Paints a rectangle behind every occurrence of the search term on the line,
    /// with the active match in a stronger colour.
    /// </summary>
    private void DrawSearchHighlights(DrawingContext drawingContext, FormattedText formatted, string text, long lineIndex, Point origin)
    {
        var term = SearchTerm;

        if (string.IsNullOrEmpty(term) || text.Length == 0)
            return;

        var comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var index = text.IndexOf(term, comparison);

        while (index >= 0)
        {
            var geometry = formatted.BuildHighlightGeometry(origin, index, term.Length);

            if (geometry is not null)
            {
                var isCurrent = CurrentMatch is { } match
                                && match.LineIndex == lineIndex
                                && match.ColumnIndex == index;

                drawingContext.DrawGeometry(isCurrent ? CurrentMatchBrush : MatchBrush, null, geometry);
            }

            var next = index + term.Length;
            if (next >= text.Length)
                return;

            index = text.IndexOf(term, next, comparison);
        }
    }

    private FormattedText CreateFormattedText(string text) => new(
        text,
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        _typeface,
        FontSize,
        Foreground,
        _pixelsPerDip);

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        AnimateByLines(-e.Delta / 120.0 * WheelLinesPerNotch);
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        // Growing the window may leave empty space below the last line.
        SetVerticalScrollPosition(VerticalScrollPosition);
        ScrollChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        UpdateFontMetrics();
    }

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (TextView)d;
        view.StopAnimation();
        view._firstVisibleLine = 0;
        view._lineOffset = 0;
        view._horizontalOffset = 0;
        view.FirstVisibleLine = 0;
        view.ScrollChanged?.Invoke(view, EventArgs.Empty);
    }

    private static void OnLineCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TextView)d).ScrollChanged?.Invoke(d, EventArgs.Empty);

    private static void OnCurrentMatchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is SearchMatch match)
            ((TextView)d).EnsureLineVisible(match.LineIndex);
    }

    private static void OnFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TextView)d).UpdateFontMetrics();

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void UpdateFontMetrics()
    {
        _typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        // One sample line gives the exact height used for every row.
        var sample = CreateFormattedText("Mg");
        _lineHeight = Math.Max(1, Math.Ceiling(sample.Height));

        InvalidateVisual();
        ScrollChanged?.Invoke(this, EventArgs.Empty);
    }
}
