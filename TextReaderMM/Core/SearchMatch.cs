namespace TextReaderMM.Core;

/// <summary>One occurrence of the search term: which line, where in it, and how long.</summary>
public readonly record struct SearchMatch(long LineIndex, int ColumnIndex, int Length);
