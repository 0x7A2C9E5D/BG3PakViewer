using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Loader;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

public class LocalizationPreviewHandler(IAppSettings appSettings) : TextBasedPreviewHandler(appSettings)
{
    public override bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsLocalizationFormat(fileExtension);
    }

    protected override async Task<string?> GetTextAsync(Stream stream, string fileExtension)
    {
        var resource = await LocalizationLoader.LoadAsync(stream);
        return resource == null ? null : await LocalizationLoader.ExportAsync(resource);
    }
}