using System.Windows;
using TextReaderMM.ViewModels;

namespace TextReaderMM;

/// <summary>
/// Composition root: builds the window and its view model on startup.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();

        // DialogService needs the window as dialog owner, so the view model is built after it.
        window.DataContext = new MainViewModel(new DialogService(window));

        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Closes the indexed file and removes any downloaded or generated temp file.
        (MainWindow?.DataContext as IDisposable)?.Dispose();

        base.OnExit(e);
    }
}
