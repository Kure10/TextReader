using System.Windows;
using System.Windows.Input;

namespace TextReaderMM.Views;

public partial class UrlInputWindow : Window
{
    public UrlInputWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            UrlBox.Focus();
            UrlBox.CaretIndex = UrlBox.Text.Length;
        };
    }

    public string Url => UrlBox.Text.Trim();

    private void OnOkClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnUrlBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        DialogResult = true;
        e.Handled = true;
    }
}
