using System.IO;
using System.Text;

namespace TextReaderMM.Core;

/// <summary>Text encodings supported by the reader.</summary>
public enum TextEncodingKind
{
    /// <summary>UTF-8 (also covers plain ASCII). One line break byte: 0x0A.</summary>
    Utf8,

    /// <summary>UTF-16 little endian. Line break is 0x0A 0x00.</summary>
    Utf16Le,

    /// <summary>UTF-16 big endian. Line break is 0x00 0x0A.</summary>
    Utf16Be
}

public static class TextEncodingDetector
{
    /// <summary>
    /// Detects the encoding from the byte order mark, falling back to a simple
    /// heuristic over the first few kilobytes. Also reports how many bytes the
    /// BOM occupies, so the text data starts right after it.
    /// </summary>
    public static TextEncodingKind Detect(Stream stream, out int preambleLength)
    {
        var header = new byte[4096];
        stream.Position = 0;
        var read = stream.Read(header, 0, header.Length);

        if (read >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
        {
            preambleLength = 3;
            return TextEncodingKind.Utf8;
        }

        if (read >= 2 && header[0] == 0xFF && header[1] == 0xFE)
        {
            preambleLength = 2;
            return TextEncodingKind.Utf16Le;
        }

        if (read >= 2 && header[0] == 0xFE && header[1] == 0xFF)
        {
            preambleLength = 2;
            return TextEncodingKind.Utf16Be;
        }

        preambleLength = 0;
        return GuessUtf16WithoutBom(header.AsSpan(0, read));
    }

    /// <summary>
    /// UTF-16 text of western languages contains a zero byte in every character.
    /// Their position tells the byte order apart; without them we assume UTF-8.
    /// </summary>
    private static TextEncodingKind GuessUtf16WithoutBom(ReadOnlySpan<byte> header)
    {
        var zerosAtOdd = 0;
        var zerosAtEven = 0;

        for (var i = 0; i < header.Length; i++)
        {
            if (header[i] != 0)
                continue;

            if (i % 2 == 0)
                zerosAtEven++;
            else
                zerosAtOdd++;
        }

        var threshold = header.Length / 8;

        if (zerosAtOdd > threshold && zerosAtOdd > zerosAtEven * 4)
            return TextEncodingKind.Utf16Le;

        if (zerosAtEven > threshold && zerosAtEven > zerosAtOdd * 4)
            return TextEncodingKind.Utf16Be;

        return TextEncodingKind.Utf8;
    }

    public static Encoding ToEncoding(TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Utf16Le => Encoding.Unicode,
        TextEncodingKind.Utf16Be => Encoding.BigEndianUnicode,
        _ => Encoding.UTF8
    };
}
