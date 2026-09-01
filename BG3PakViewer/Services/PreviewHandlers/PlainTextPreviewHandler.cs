using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

internal class PlainTextPreviewHandler(IPackageService packageService, IAppSettings appSettings)
    : TextBasedPreviewHandler(packageService, appSettings)
{
    public override bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsPlainText(fileExtension);
    }

    protected override async Task<string?> GetTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, false);
        return await reader.ReadToEndAsync();
    }
}