using System.Windows;
using BG3PakViewer.Contracts;
using Serilog;

namespace BG3PakViewer.Services;

/// <summary>
///     Clipboard service
/// </summary>
internal class ClipboardService : IClipboardService
{
    /// <summary>
    ///     Try set text
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public bool TrySetText(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to copy text to clipboard");
            return false;
        }
    }
}