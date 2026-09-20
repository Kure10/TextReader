using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace TextReaderMM.Views;

public partial class GoToLineWindow : Window
{
    private readonly long _maxLine;

    public GoToLineWindow(long maxLine, long currentLine)
    {
        InitializeComponent();

        _maxLine = Math.Max(1, maxLine);
        RangeHint.Text = $"1 - {_maxLine:N0}";
        LineBox.Text = (currentLine + 1).ToString(CultureInfo.InvariantCulture);

        Loaded += (_, _) =>
        {
            LineBox.Focus();
            LineBox.SelectAll();
        };
    }

    /// <summary>Zero-based index of the chosen line.</summary>
    public long LineIndex { get; private set; }

    private void OnOkClick(object sender, RoutedEventArgs e) => Confirm();

    private void OnLineBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        Confirm();
        e.Handled = true;
    }

    private void Confirm()
    {
        var text = LineBox.Text.Replace(" ", string.Empty).Replace(",", string.Empty);

        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var line) || line < 1)
        {
            MessageBox.Show(this, $"Enter a line number between 1 and {_maxLine:N0}.", "Go to Line",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Lines beyond the end (or beyond what is indexed so far) land on the last known line.
        LineIndex = Math.Min(line, _maxLine) - 1;
        DialogResult = true;
    }
}
