using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Loader;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

public class LarianResourcePreviewHandler(IAppSettings appSettings) : TextBasedPreviewHandler(appSettings)
{
    public override bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsLarianBinaryResource(fileExtension);
    }

    protected override async Task<string?> GetTextAsync(Stream stream, string fileExtension)
    {
        var resource = await ResourceLoader.LoadAsync(stream, fileExtension);
        return resource == null ? null : await ResourceLoader.ExportAsync(resource);
    }
}