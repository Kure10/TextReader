namespace TextReaderMM.Core.Interfaces;

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
