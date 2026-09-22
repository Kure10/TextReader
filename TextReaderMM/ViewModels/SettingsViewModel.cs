using System.Windows.Input;
using TextReaderMM.Core;

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

    private const int MinCopyCharacters = 1000;
    private const int MaxCopyCharactersLimit = 50_000;
    private const double MinScrollSpeed = 4;
    private const double MaxScrollSpeed = 30;

    private double _fontSize = DefaultFontSize;
    private bool _showLineNumbers = true;
    private int _maxCopyCharacters = 5000;
    private double _scrollSpeed = 14;

    public SettingsViewModel(UserSettings? saved = null)
    {
        // Values from the file are clamped as well: the file can be edited by hand.
        if (saved is not null)
        {
            FontSize = saved.FontSize;
            ShowLineNumbers = saved.ShowLineNumbers;
            MaxCopyCharacters = saved.MaxCopyCharacters;
            ScrollSpeed = saved.ScrollSpeed;
        }

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

    /// <summary>
    /// Speed of the scrolling animation: higher snaps to the target sooner,
    /// lower glides longer. It is the rate of the exponential easing in TextView.
    /// </summary>
    public double ScrollSpeed
    {
        get => _scrollSpeed;
        set => SetField(ref _scrollSpeed, Math.Clamp(value, MinScrollSpeed, MaxScrollSpeed));
    }

    /// <summary>How much text a single copy may put on the clipboard.</summary>
    public int MaxCopyCharacters
    {
        get => _maxCopyCharacters;
        set => SetField(ref _maxCopyCharacters, Math.Clamp(value, MinCopyCharacters, MaxCopyCharactersLimit));
    }

    /// <summary>Current state in the shape that gets written to disk.</summary>
    public UserSettings ToUserSettings() => new(FontSize, ShowLineNumbers, MaxCopyCharacters, ScrollSpeed);
}
