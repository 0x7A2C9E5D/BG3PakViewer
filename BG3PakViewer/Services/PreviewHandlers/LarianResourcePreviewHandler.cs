using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Loader;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

public class LarianResourcePreviewHandler : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsLarianBinaryResource(fileExtension);
    }

    public async Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension)
    {
        var resource = await ResourceLoader.LoadAsync(stream, fileExtension);

        if (resource == null)
            return null;

        var text = await ResourceLoader.ExportAsync(resource);

        return string.IsNullOrEmpty(text)
            ? null
            : new PlainTextFilePreviewViewModel { Data = text };
    }
}