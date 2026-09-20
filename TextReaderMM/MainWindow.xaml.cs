using System.Windows;
using System.Windows.Input;
using TextReaderMM.ViewModels;

namespace TextReaderMM;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    /// <summary>Ctrl+F shows the search bar and puts the caret straight into it.</summary>
    private void OnFindExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        ViewModel.Search.IsVisible = true;

        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is null)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                var command = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                    ? ViewModel.Search.FindPreviousCommand
                    : ViewModel.Search.FindNextCommand;

                if (command.CanExecute(null))
                    command.Execute(null);

                e.Handled = true;
                break;

            case Key.Escape:
                CloseSearch();
                e.Handled = true;
                break;
        }
    }

    private void OnCloseSearchClick(object sender, RoutedEventArgs e) => CloseSearch();

    private void CloseSearch()
    {
        if (ViewModel is null)
            return;

        ViewModel.Search.IsVisible = false;
        Viewer.FocusText();
    }
}
