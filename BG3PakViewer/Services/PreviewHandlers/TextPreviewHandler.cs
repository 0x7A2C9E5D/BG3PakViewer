using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

public class TextPreviewHandler(IAppSettings appSettings) : TextBasedPreviewHandler(appSettings)
{
    public override bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsPlainText(fileExtension);
    }

    protected override async Task<string?> GetTextAsync(Stream stream, string fileExtension)
    {
        using var reader = new StreamReader(stream, false);
        return await reader.ReadToEndAsync();
    }
}