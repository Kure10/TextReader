namespace TextReaderMM.Core.Interfaces;

/// <summary>
/// Read-only view of a (potentially huge) text document split into lines.
/// The UI and the search work only against this abstraction, never against a file.
/// </summary>
public interface ITextDocument : IDisposable
{
    /// <summary>Path of the underlying file on disk.</summary>
    string FilePath { get; }

    /// <summary>Size of the underlying file in bytes.</summary>
    long FileSize { get; }

    /// <summary>Detected encoding of the file.</summary>
    TextEncodingKind Encoding { get; }

    /// <summary>Number of lines known so far; grows while indexing runs.</summary>
    long LineCount { get; }

    /// <summary>True once the whole file has been indexed.</summary>
    bool IsIndexingComplete { get; }

    /// <summary>Reads the text of the line at the given zero-based index.</summary>
    string GetLine(long index);
}
