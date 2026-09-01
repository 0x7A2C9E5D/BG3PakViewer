namespace BG3PakViewer.Contracts;

public interface IClipboardService
{
    public bool TrySetText(string text);
}