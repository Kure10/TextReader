using System.Windows.Input;

namespace TextReaderMM.Commands;

/// <summary>
/// Every application-level command with its keyboard shortcut in one place.
/// Menu items take their gesture text straight from here, so the menu and the
/// keyboard can never drift apart.
/// Navigation inside the text (arrows, PageUp/Down, Home/End) deliberately stays
/// in TextView: that is movement within a control, not a command of the application.
/// </summary>
public static class AppCommands
{
    public static readonly RoutedUICommand OpenFile = Create(
        "Open file", nameof(OpenFile), new KeyGesture(Key.O, ModifierKeys.Control));

    public static readonly RoutedUICommand OpenUrl = Create(
        "Open URL", nameof(OpenUrl), new KeyGesture(Key.U, ModifierKeys.Control));

    public static readonly RoutedUICommand GenerateRandomText = Create(
        "Generate random text", nameof(GenerateRandomText), new KeyGesture(Key.R, ModifierKeys.Control));

    public static readonly RoutedUICommand SaveAs = Create(
        "Save as", nameof(SaveAs), new KeyGesture(Key.S, ModifierKeys.Control));

    public static readonly RoutedUICommand Find = Create(
        "Find", nameof(Find), new KeyGesture(Key.F, ModifierKeys.Control));

    public static readonly RoutedUICommand FindNext = Create(
        "Find next", nameof(FindNext), new KeyGesture(Key.F3));

    public static readonly RoutedUICommand FindPrevious = Create(
        "Find previous", nameof(FindPrevious), new KeyGesture(Key.F3, ModifierKeys.Shift));

    public static readonly RoutedUICommand GoToLine = Create(
        "Go to line", nameof(GoToLine), new KeyGesture(Key.G, ModifierKeys.Control));

    private static RoutedUICommand Create(string text, string name, params KeyGesture[] gestures)
    {
        var collection = new InputGestureCollection();

        foreach (var gesture in gestures)
            collection.Add(gesture);

        return new RoutedUICommand(text, name, typeof(AppCommands), collection);
    }
}
