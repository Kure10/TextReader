namespace TextReaderMM.Controls;

/// <summary>A caret position: a line and a character index within it.</summary>
public readonly record struct TextPosition(long Line, int Column) : IComparable<TextPosition>
{
    public int CompareTo(TextPosition other)
    {
        var byLine = Line.CompareTo(other.Line);
        return byLine != 0 ? byLine : Column.CompareTo(other.Column);
    }

    public static bool operator <(TextPosition left, TextPosition right) => left.CompareTo(right) < 0;
    public static bool operator >(TextPosition left, TextPosition right) => left.CompareTo(right) > 0;
    public static bool operator <=(TextPosition left, TextPosition right) => left.CompareTo(right) <= 0;
    public static bool operator >=(TextPosition left, TextPosition right) => left.CompareTo(right) >= 0;
}
