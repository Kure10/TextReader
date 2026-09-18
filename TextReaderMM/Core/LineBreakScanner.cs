namespace TextReaderMM.Core;

/// <summary>
/// Finds line breaks in a raw byte buffer. One implementation per encoding,
/// so the indexer never has to know how a line break is encoded.
/// </summary>
public interface ILineBreakScanner
{
    /// <summary>
    /// Index of the next line break inside <paramref name="buffer"/> at or after
    /// <paramref name="startIndex"/>, or -1 when the buffer holds none.
    /// <paramref name="bufferDataOffset"/> is the offset of buffer[0] measured from
    /// the start of the text data (i.e. the file offset minus the BOM length);
    /// UTF-16 needs it to tell which bytes are even and which are odd.
    /// </summary>
    int FindLineBreak(ReadOnlySpan<byte> buffer, int startIndex, long bufferDataOffset);

    /// <summary>
    /// How many bytes to skip from the found line break to reach the next line.
    /// </summary>
    int BytesToNextLine { get; }
}

/// <summary>UTF-8 and ASCII: the line break is the single byte 0x0A.</summary>
public sealed class Utf8LineBreakScanner : ILineBreakScanner
{
    public int BytesToNextLine => 1;

    public int FindLineBreak(ReadOnlySpan<byte> buffer, int startIndex, long bufferDataOffset)
    {
        var found = buffer[startIndex..].IndexOf((byte)0x0A);
        return found < 0 ? -1 : startIndex + found;
    }
}

/// <summary>
/// UTF-16 little endian: the line break is 0x0A 0x00, so the 0x0A byte always
/// sits at an even offset. A 0x0A at an odd offset is the high byte of some
/// other character and must be ignored.
/// </summary>
public sealed class Utf16LeLineBreakScanner : ILineBreakScanner
{
    public int BytesToNextLine => 2;

    public int FindLineBreak(ReadOnlySpan<byte> buffer, int startIndex, long bufferDataOffset)
        => Utf16ScanHelper.FindLineBreak(buffer, startIndex, bufferDataOffset, expectedParity: 0);
}

/// <summary>
/// UTF-16 big endian: the line break is 0x00 0x0A, so the 0x0A byte always
/// sits at an odd offset.
/// </summary>
public sealed class Utf16BeLineBreakScanner : ILineBreakScanner
{
    public int BytesToNextLine => 1;

    public int FindLineBreak(ReadOnlySpan<byte> buffer, int startIndex, long bufferDataOffset)
        => Utf16ScanHelper.FindLineBreak(buffer, startIndex, bufferDataOffset, expectedParity: 1);
}

internal static class Utf16ScanHelper
{
    public static int FindLineBreak(ReadOnlySpan<byte> buffer, int startIndex, long bufferDataOffset, int expectedParity)
    {
        var searchFrom = startIndex;

        while (searchFrom < buffer.Length)
        {
            var found = buffer[searchFrom..].IndexOf((byte)0x0A);
            if (found < 0)
                return -1;

            var index = searchFrom + found;
            if ((bufferDataOffset + index) % 2 == expectedParity)
                return index;

            searchFrom = index + 1;
        }

        return -1;
    }
}

public static class LineBreakScannerFactory
{
    public static ILineBreakScanner Create(TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Utf16Le => new Utf16LeLineBreakScanner(),
        TextEncodingKind.Utf16Be => new Utf16BeLineBreakScanner(),
        _ => new Utf8LineBreakScanner()
    };
}
