using System.Windows;
using TextReaderMM.ViewModels;

namespace TextReaderMM;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new DialogService(this));
    }
}
