using System.Globalization;
using System.Windows;

namespace TextReaderMM.Views;

public partial class GenerateTextWindow : Window
{
    private const long MaxLines = 100_000_000;

    public GenerateTextWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            LineCountBox.Focus();
            LineCountBox.SelectAll();
        };
    }

    public long LineCount { get; private set; }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var text = LineCountBox.Text.Replace(" ", string.Empty);

        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lines) || lines <= 0 || lines > MaxLines)
        {
            MessageBox.Show(this, $"Enter a number between 1 and {MaxLines:N0}.", "Generate random text",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LineCount = lines;
        DialogResult = true;
    }
}
