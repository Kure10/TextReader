namespace TextReaderMM.ViewModels.Interfaces;

/// <summary>
/// A window showing progress of a long operation. Disposing it closes the window.
/// </summary>
public interface IProgressDialog : IDisposable
{
    void Report(double ratio, string text);
}
