using System.ComponentModel;
using System.Windows;
using TextReaderMM.ViewModels.Interfaces;

namespace TextReaderMM.Views;

/// <summary>
/// Shows the progress of a long running operation and lets the user cancel it.
/// Closing the window means cancel as well.
/// </summary>
public partial class ProgressWindow : Window, IProgressDialog
{
    private readonly CancellationTokenSource _cts;
    private bool _closedByCode;

    public ProgressWindow(string title, CancellationTokenSource cts)
    {
        InitializeComponent();

        Title = title;
        StatusText.Text = title;
        _cts = cts;
    }

    public void Report(double ratio, string text)
    {
        Progress.Value = Math.Clamp(ratio, 0, 1);
        StatusText.Text = text;
    }

    public void Dispose()
    {
        _closedByCode = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => _cts.Cancel();

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (!_closedByCode)
            _cts.Cancel();
    }
}
