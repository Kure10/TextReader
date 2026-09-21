using System.Windows;
using System.Windows.Input;
using TextReaderMM.Commands;
using TextReaderMM.ViewModels;

namespace TextReaderMM;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel old)
            old.ScrollToLineRequested -= OnScrollToLineRequested;

        CommandBindings.Clear();

        if (e.NewValue is not MainViewModel viewModel)
            return;

        // The view model decides where to go, the view does the scrolling.
        viewModel.ScrollToLineRequested += OnScrollToLineRequested;

        // Thin glue: a routed command (menu or shortcut) runs the view model command.
        Bind(AppCommands.OpenFile, viewModel.OpenFileCommand);
        Bind(AppCommands.OpenUrl, viewModel.OpenUrlCommand);
        Bind(AppCommands.GenerateRandomText, viewModel.GenerateRandomTextCommand);
        Bind(AppCommands.SaveAs, viewModel.SaveAsCommand);
        Bind(AppCommands.GoToLine, viewModel.GoToLineCommand);
        Bind(AppCommands.IncreaseFontSize, viewModel.Settings.IncreaseFontSizeCommand);
        Bind(AppCommands.DecreaseFontSize, viewModel.Settings.DecreaseFontSizeCommand);
        Bind(AppCommands.ResetFontSize, viewModel.Settings.ResetFontSizeCommand);
        Bind(AppCommands.FindNext, viewModel.Search.FindNextCommand);
        Bind(AppCommands.FindPrevious, viewModel.Search.FindPreviousCommand);

        // Showing the search bar also moves the focus, which is a view concern.
        CommandBindings.Add(new CommandBinding(AppCommands.Find, (_, _) => ShowSearchBar()));
    }

    private void Bind(RoutedUICommand command, ICommand target)
        => CommandBindings.Add(new CommandBinding(
            command,
            (_, _) => target.Execute(null),
            (_, args) => args.CanExecute = target.CanExecute(null)));

    private void OnScrollToLineRequested(object? sender, long line) => Viewer.ScrollToLine(line);

    private void ShowSearchBar()
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
