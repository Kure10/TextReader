using System.Globalization;
using System.Reflection;
using System.Windows.Input;

namespace TextReaderMM.Commands;

public sealed record ShortcutInfo(string Group, string Gesture, string Description);

/// <summary>
/// Builds the list shown in the shortcut legend. Commands are read out of
/// <see cref="AppCommands"/> itself, so a new command appears in the legend without
/// anyone having to remember to add it. What cannot be read that way — keys handled
/// directly by TextView and the mouse — is listed here by hand.
/// </summary>
public static class ShortcutCatalog
{
    public static IReadOnlyList<ShortcutInfo> GetAll()
    {
        var shortcuts = new List<ShortcutInfo>();

        shortcuts.AddRange(GetApplicationCommands());

        shortcuts.AddRange(
        [
            new ShortcutInfo("Navigation", "Up / Down", "Move one line"),
            new ShortcutInfo("Navigation", "Page Up / Page Down", "Move one screen"),
            new ShortcutInfo("Navigation", "Home / End", "Start / end of the document"),
            new ShortcutInfo("Navigation", "Left / Right", "Scroll sideways"),
            new ShortcutInfo("Navigation", "Mouse wheel", "Scroll"),

            new ShortcutInfo("Selection", "Drag with the left button", "Select text"),
            new ShortcutInfo("Selection", "Shift + click", "Extend the selection"),
            new ShortcutInfo("Selection", "Ctrl + C", "Copy the selection"),
            new ShortcutInfo("Selection", "Ctrl + A", "Select the whole document"),

            new ShortcutInfo("Line numbers", "Drag the separator", "Resize the line number column"),
            new ShortcutInfo("Line numbers", "Double click the separator", "Back to the automatic width")
        ]);

        return shortcuts;
    }

    private static IEnumerable<ShortcutInfo> GetApplicationCommands()
    {
        var fields = typeof(AppCommands).GetFields(BindingFlags.Public | BindingFlags.Static);

        foreach (var field in fields)
        {
            if (field.GetValue(null) is not RoutedUICommand command)
                continue;

            var gestures = command.InputGestures
                .OfType<KeyGesture>()
                .Select(gesture => gesture.GetDisplayStringForCulture(CultureInfo.CurrentCulture));

            yield return new ShortcutInfo("Commands", string.Join(" / ", gestures), command.Text);
        }
    }
}
