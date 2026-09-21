using System.Globalization;
using System.Text;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TextReaderMM.Core;
using TextReaderMM.Core.Interfaces;
using TextReaderMM.Diagnostics;
using TextReaderMM.Diagnostics;

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

    /// <summary>Padding on both sides of the line numbers.</summary>
    private const double GutterPadding = 10;

    /// <summary>How close to the separator the mouse has to be to start dragging it.</summary>
    private const double GutterGripWidth = 4;

    /// <summary>The gutter never gets narrower than this, so the separator stays grabbable.</summary>
    private const double MinGutterWidth = 30;

    /// <summary>Upper bound for the gutter; wider than this it only steals space from the text.</summary>
    private const double MaxGutterWidth = 110;

    /// <summary>Default cap for copying; a selection can span the whole document.</summary>
    public const int DefaultMaxCopyCharacters = 5000;

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

    /// <summary>
    /// A selection can span the whole document, which is far more than the clipboard
    /// (or memory) can take, so copying stops at this many characters.
    /// </summary>
    public static readonly DependencyProperty MaxCopyCharactersProperty = DependencyProperty.Register(
        nameof(MaxCopyCharacters), typeof(int), typeof(TextView),
        new FrameworkPropertyMetadata(DefaultMaxCopyCharacters));

    public static readonly DependencyProperty ShowLineNumbersProperty = DependencyProperty.Register(
        nameof(ShowLineNumbers), typeof(bool), typeof(TextView),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

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

    private static readonly Brush GutterBackground = CreateFrozenBrush(Color.FromRgb(247, 247, 247));
    private static readonly Brush GutterForeground = CreateFrozenBrush(Color.FromRgb(150, 150, 150));
    private static readonly Pen GutterSeparator = CreateFrozenPen(Color.FromRgb(210, 210, 210));

    private static readonly Brush SelectionBrush = CreateFrozenBrush(Color.FromRgb(173, 214, 255));

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

    // Line number gutter. Null width means "as wide as the largest line number needs".
    private double? _manualGutterWidth;
    private bool _isDraggingGutter;

    // Selection. Anchor is where the drag started, caret is where the mouse is now.
    private TextPosition? _selectionAnchor;
    private TextPosition _selectionCaret;
    private bool _isSelecting;
    private double _charWidth = 8;
    private readonly DispatcherTimer _autoScrollTimer;
    private double _autoScrollLines;

    public TextView()
    {
        Focusable = true;
        ClipToBounds = true;

        // Dragging past the top or bottom edge keeps scrolling while the mouse stays there.
        _autoScrollTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(30) };
        _autoScrollTimer.Tick += OnAutoScrollTick;

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopy, OnCanCopy));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, OnSelectAll, OnCanSelectAll));

        ContextMenu = new ContextMenu
        {
            Items =
            {
                new MenuItem { Command = ApplicationCommands.Copy },
                new MenuItem { Command = ApplicationCommands.SelectAll }
            }
        };
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

    public bool ShowLineNumbers
    {
        get => (bool)GetValue(ShowLineNumbersProperty);
        set => SetValue(ShowLineNumbersProperty, value);
    }

    public int MaxCopyCharacters
    {
        get => (int)GetValue(MaxCopyCharactersProperty);
        set => SetValue(MaxCopyCharactersProperty, value);
    }

    public double LineHeight => _lineHeight;

    /// <summary>
    /// Width of the line number column. It fits the largest line number by default;
    /// dragging the separator overrides that. The text keeps priority: the gutter never
    /// takes more than a third of the control and numbers are clipped rather than the text.
    /// </summary>
    public double GutterWidth
    {
        get
        {
            if (!ShowLineNumbers)
                return 0;

            // Never wider than the fixed limit, and never more than a third of the control.
            var maxWidth = Math.Min(MaxGutterWidth, Math.Max(0, ActualWidth / 3));

            // In a very narrow window even the minimum does not fit; the text wins there.
            if (maxWidth <= MinGutterWidth)
                return maxWidth;

            var width = _manualGutterWidth ?? MeasureGutterWidth();
            var finalWidth = Math.Clamp(width, MinGutterWidth, maxWidth);
            return finalWidth;
        }
    }

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

    public bool HasSelection => _selectionAnchor is { } anchor && anchor != _selectionCaret;

    public void ClearSelection()
    {
        if (_selectionAnchor is null)
            return;

        _selectionAnchor = null;
        InvalidateVisual();
    }

    public void SelectAll()
    {
        var document = Document;

        if (document is null || LineCount == 0)
            return;

        var lastLine = LineCount - 1;

        _selectionAnchor = new TextPosition(0, 0);
        _selectionCaret = new TextPosition(lastLine, document.GetLine(lastLine).Length);

        InvalidateVisual();
    }

    /// <summary>
    /// Text of the current selection, cut off at <see cref="MaxCopyCharacters"/>.
    /// Returns null when nothing is selected.
    /// </summary>
    public string? GetSelectedText(out bool truncated)
    {
        truncated = false;

        var document = Document;
        if (document is null || !HasSelection)
            return null;

        var (start, end) = GetOrderedSelection();
        var limit = Math.Max(1, MaxCopyCharacters);
        var builder = new StringBuilder();

        for (var line = start.Line; line <= end.Line && line < LineCount; line++)
        {
            var text = document.GetLine(line);
            var from = line == start.Line ? Math.Min(start.Column, text.Length) : 0;
            var to = line == end.Line ? Math.Min(end.Column, text.Length) : text.Length;

            if (to > from)
                builder.Append(text, from, to - from);

            if (line != end.Line)
                builder.Append(Environment.NewLine);

            if (builder.Length >= limit)
            {
                truncated = true;
                break;
            }
        }

        return builder.ToString(0, Math.Min(builder.Length, limit));
    }

    private (TextPosition Start, TextPosition End) GetOrderedSelection()
    {
        var anchor = _selectionAnchor ?? _selectionCaret;
        return anchor <= _selectionCaret ? (anchor, _selectionCaret) : (_selectionCaret, anchor);
    }

    /// <summary>
    /// Turns a mouse position into a line and column. The column is a plain division
    /// because the font is monospaced; a proportional font would need measuring.
    /// </summary>
    private TextPosition GetPositionFromPoint(Point point)
    {
        var document = Document;

        if (document is null || LineCount == 0 || _lineHeight <= 0)
            return new TextPosition(0, 0);

        var rawLine = _firstVisibleLine + (long)Math.Floor((point.Y + _lineOffset) / _lineHeight);
        var line = Math.Clamp(rawLine, 0, LineCount - 1);

        var x = point.X - GutterWidth + _horizontalOffset;
        var column = _charWidth <= 0 ? 0 : (int)Math.Round(x / _charWidth);

        return new TextPosition(line, Math.Clamp(column, 0, document.GetLine(line).Length));
    }

    private void OnCanCopy(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = HasSelection;

    private void OnCopy(object sender, ExecutedRoutedEventArgs e)
    {
        var text = GetSelectedText(out var truncated);

        if (string.IsNullOrEmpty(text))
            return;

        try
        {
            Clipboard.SetText(text);

            if (truncated)
                Log.Warning($"Selection was longer than {MaxCopyCharacters:N0} characters and was cut off when copying.");
        }
        catch (Exception exception)
        {
            // The clipboard is shared with other processes and can be locked by them.
            Log.Error("Copying to the clipboard failed", exception);
        }
    }

    private void OnCanSelectAll(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = LineCount > 0;

    private void OnSelectAll(object sender, ExecutedRoutedEventArgs e) => SelectAll();

    private void OnAutoScrollTick(object? sender, EventArgs e)
    {
        if (!_isSelecting)
        {
            _autoScrollTimer.Stop();
            return;
        }

        VerticalScrollPosition += _autoScrollLines;
        _selectionCaret = GetPositionFromPoint(Mouse.GetPosition(this));

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawRectangle(Background, null, new Rect(RenderSize));

        var document = Document;
        if (document is null || LineCount == 0 || _lineHeight <= 0)
            return;

        _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        _widestVisibleLine = 0;

        var gutterWidth = GutterWidth;

        DrawGutterBackground(drawingContext, gutterWidth);
        DrawLines(drawingContext, document, gutterWidth);
    }

    private void DrawGutterBackground(DrawingContext drawingContext, double gutterWidth)
    {
        if (gutterWidth <= 0)
            return;

        drawingContext.DrawRectangle(GutterBackground, null, new Rect(0, 0, gutterWidth, ActualHeight));

        // Half a pixel keeps the separator crisp instead of blurred over two pixels.
        var x = Math.Round(gutterWidth) - 0.5;
        drawingContext.DrawLine(GutterSeparator, new Point(x, 0), new Point(x, ActualHeight));
    }

    private void DrawLines(DrawingContext drawingContext, ITextDocument document, double gutterWidth)
    {
        var y = -_lineOffset;
        var lineIndex = _firstVisibleLine;

        while (y < ActualHeight && lineIndex < LineCount)
        {
            if (gutterWidth > 0)
                DrawLineNumber(drawingContext, document, lineIndex, y, gutterWidth);

            var text = document.GetLine(lineIndex);
            var formatted = CreateFormattedText(text, Foreground);
            var origin = new Point(gutterWidth - _horizontalOffset, y);

            _widestVisibleLine = Math.Max(_widestVisibleLine, formatted.WidthIncludingTrailingWhitespace);

            // Text must never spill over the gutter when scrolled horizontally.
            drawingContext.PushClip(new RectangleGeometry(new Rect(gutterWidth, 0, Math.Max(0, ActualWidth - gutterWidth), ActualHeight)));

            DrawSelection(drawingContext, formatted, text, lineIndex, origin);
            DrawSearchHighlights(drawingContext, formatted, text, lineIndex, origin);
            drawingContext.DrawText(formatted, origin);

            drawingContext.Pop();

            y += _lineHeight;
            lineIndex++;
        }
    }

    /// <summary>
    /// Numbers are right aligned next to the separator. A number too wide for the gutter
    /// is clipped on the left, so its last digits stay readable and the text is untouched.
    /// </summary>
    private void DrawLineNumber(DrawingContext drawingContext, ITextDocument document, long lineIndex, double y, double gutterWidth)
    {
        // Under an active filter the original line numbers are the useful ones.
        var displayedLine = document is FilteredTextDocument filtered
            ? filtered.GetSourceLine(lineIndex) + 1
            : lineIndex + 1;

        var formatted = CreateFormattedText(displayedLine.ToString(), GutterForeground);
        var x = gutterWidth - GutterPadding - formatted.Width;

        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, Math.Max(0, gutterWidth - GutterPadding / 2), ActualHeight)));
        drawingContext.DrawText(formatted, new Point(x, y));
        drawingContext.Pop();
    }

    /// <summary>Width needed by the largest line number the document can show.</summary>
    private double MeasureGutterWidth()
    {
        var digits = Math.Max(2, LineCount.ToString().Length);
        var sample = CreateFormattedText(new string('0', digits), GutterForeground);

        return sample.Width + GutterPadding * 2;
    }

    private void DrawSelection(DrawingContext drawingContext, FormattedText formatted, string text, long lineIndex, Point origin)
    {
        if (!HasSelection)
            return;

        var (start, end) = GetOrderedSelection();

        if (lineIndex < start.Line || lineIndex > end.Line)
            return;

        var from = lineIndex == start.Line ? Math.Min(start.Column, text.Length) : 0;
        var to = lineIndex == end.Line ? Math.Min(end.Column, text.Length) : text.Length;

        if (to <= from)
            return;

        var geometry = formatted.BuildHighlightGeometry(origin, from, to - from);

        if (geometry is not null)
            drawingContext.DrawGeometry(SelectionBrush, null, geometry);
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

    private FormattedText CreateFormattedText(string text, Brush brush) => new(
        text,
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        _typeface,
        FontSize,
        brush,
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

        if (e.ChangedButton != MouseButton.Left)
            return;

        var position = e.GetPosition(this);

        if (IsOverGutterSeparator(position.X))
        {
            // Double click on the separator goes back to the automatic width.
            if (e.ClickCount == 2)
            {
                _manualGutterWidth = null;
                InvalidateVisual();
            }
            else
            {
                _isDraggingGutter = true;
                CaptureMouse();
            }

            e.Handled = true;
            return;
        }

        if (position.X < GutterWidth)
            return;

        StartSelection(position, extend: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        e.Handled = true;
    }

    private void StartSelection(Point position, bool extend)
    {
        var caret = GetPositionFromPoint(position);

        // Shift keeps the existing anchor, so the selection grows instead of starting over.
        if (!extend || _selectionAnchor is null)
            _selectionAnchor = caret;

        _selectionCaret = caret;
        _isSelecting = true;

        CaptureMouse();
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var x = e.GetPosition(this).X;

        if (_isDraggingGutter)
        {
            _manualGutterWidth = Math.Max(0, x);
            InvalidateVisual();
            return;
        }

        if (_isSelecting)
        {
            ExtendSelection(e.GetPosition(this));
            return;
        }

        Cursor = IsOverGutterSeparator(x) ? Cursors.SizeWE
            : x < GutterWidth ? Cursors.Arrow
            : Cursors.IBeam;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isSelecting)
        {
            _isSelecting = false;
            _autoScrollTimer.Stop();
            ReleaseMouseCapture();
            return;
        }

        if (!_isDraggingGutter)
            return;

        _isDraggingGutter = false;
        ReleaseMouseCapture();
    }

    private void ExtendSelection(Point position)
    {
        _selectionCaret = GetPositionFromPoint(position);

        // Outside the viewport the view keeps scrolling on a timer, one line per tick.
        _autoScrollLines = position.Y < 0 ? -1 : position.Y > ActualHeight ? 1 : 0;

        if (_autoScrollLines == 0)
            _autoScrollTimer.Stop();
        else if (!_autoScrollTimer.IsEnabled)
            _autoScrollTimer.Start();

        InvalidateVisual();
    }

    private bool IsOverGutterSeparator(double x)
    {
        var gutterWidth = GutterWidth;
        return gutterWidth > 0 && Math.Abs(x - gutterWidth) <= GutterGripWidth;
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
        view._selectionAnchor = null;
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

    private static Pen CreateFrozenPen(Color color)
    {
        var pen = new Pen(CreateFrozenBrush(color), 1);
        pen.Freeze();
        return pen;
    }

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
        var sample = CreateFormattedText("Mg", Foreground);
        _lineHeight = Math.Max(1, Math.Ceiling(sample.Height));

        // The font is monospaced, so one character is enough to know every column position.
        _charWidth = Math.Max(1, CreateFormattedText("0", Foreground).WidthIncludingTrailingWhitespace);

        InvalidateVisual();
        ScrollChanged?.Invoke(this, EventArgs.Empty);
    }
}
