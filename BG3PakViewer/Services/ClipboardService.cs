using System.Windows;
using BG3PakViewer.Contracts;
using Serilog;

namespace BG3PakViewer.Services;

internal class ClipboardService : IClipboardService
{
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
