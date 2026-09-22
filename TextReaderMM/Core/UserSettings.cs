namespace TextReaderMM.Core;

/// <summary>Everything the application remembers between runs.</summary>
public sealed record UserSettings(
    double FontSize,
    bool ShowLineNumbers,
    int MaxCopyCharacters,
    double ScrollSpeed);
