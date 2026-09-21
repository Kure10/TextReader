using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using TextReaderMM.Commands;

namespace TextReaderMM.Views;

public partial class ShortcutsWindow : Window
{
    public ShortcutsWindow()
    {
        InitializeComponent();

        var shortcuts = new CollectionViewSource { Source = ShortcutCatalog.GetAll() };
        shortcuts.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ShortcutInfo.Group)));

        ShortcutList.ItemsSource = shortcuts.View;
    }
}
