using System.Collections;
using TextReaderMM.Core;

namespace TextReaderMM.ViewModels;

/// <summary>
/// TEMPORARY bridge between <see cref="ITextDocument"/> and the placeholder ListBox.
/// It pretends to be a list, but reads a line from disk only when the UI asks for it,
/// so a virtualizing ListBox touches just the visible items.
/// Replaced by the custom text view in step 3.
/// </summary>
public sealed class VirtualLineList(ITextDocument document, long lineCount) : IReadOnlyList<string>, IList
{
    // WPF indexes items with int, so a document longer than int.MaxValue lines is clamped here.
    public int Count { get; } = (int)Math.Min(lineCount, int.MaxValue);

    public string this[int index] => document.GetLine(index);

    object? IList.this[int index]
    {
        get => document.GetLine(index);
        set => throw new NotSupportedException();
    }

    public IEnumerator<string> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
            yield return document.GetLine(i);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    bool IList.IsFixedSize => true;
    bool IList.IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;

    int IList.Add(object? value) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
    bool IList.Contains(object? value) => false;
    int IList.IndexOf(object? value) => -1;
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();

    void ICollection.CopyTo(Array array, int index)
    {
        for (var i = 0; i < Count; i++)
            array.SetValue(document.GetLine(i), index + i);
    }
}
