using System.Windows.Input;

namespace TextReaderMM.ViewModels;

/// <summary>
/// View settings the user can change at run time. Kept apart from MainViewModel,
/// which owns the document, because these only affect how the text is displayed.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    public const double DefaultFontSize = 14;

    private const double MinFontSize = 8;
    private const double MaxFontSize = 40;
    private const double FontSizeStep = 1;

    private double _fontSize = DefaultFontSize;
    private bool _showLineNumbers = true;
    private int _maxCopyCharacters = 5000;

    public SettingsViewModel()
    {
        IncreaseFontSizeCommand = new RelayCommand(() => FontSize += FontSizeStep, () => FontSize < MaxFontSize);
        DecreaseFontSizeCommand = new RelayCommand(() => FontSize -= FontSizeStep, () => FontSize > MinFontSize);
        ResetFontSizeCommand = new RelayCommand(() => FontSize = DefaultFontSize, () => Math.Abs(FontSize - DefaultFontSize) > 0.01);
    }

    public ICommand IncreaseFontSizeCommand { get; }
    public ICommand DecreaseFontSizeCommand { get; }
    public ICommand ResetFontSizeCommand { get; }

    public double FontSize
    {
        get => _fontSize;
        set
        {
            if (!SetField(ref _fontSize, Math.Clamp(value, MinFontSize, MaxFontSize)))
                return;

            OnPropertyChanged(nameof(FontSizeText));
        }
    }

    /// <summary>Shown in the menu so the current size is visible without opening anything.</summary>
    public string FontSizeText => $"Font size: {FontSize:0} px";

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set => SetField(ref _showLineNumbers, value);
    }

    /// <summary>How much text a single copy may put on the clipboard.</summary>
    public int MaxCopyCharacters
    {
        get => _maxCopyCharacters;
        set => SetField(ref _maxCopyCharacters, value);
    }
}
