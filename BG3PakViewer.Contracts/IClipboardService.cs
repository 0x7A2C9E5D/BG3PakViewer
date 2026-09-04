namespace BG3PakViewer.Contracts;

/// <summary>
///     Clipboard service
/// </summary>
public interface IClipboardService
{
    /// <summary>
    ///     Try set text
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public bool TrySetText(string text);
}