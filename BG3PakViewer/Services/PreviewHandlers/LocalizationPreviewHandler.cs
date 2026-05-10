using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Loader;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

public class LocalizationPreviewHandler : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsLocalizationFormat(fileExtension);
    }

    public async Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension)
    {
        var resource = await LocalizationLoader.LoadAsync(stream);

        if (resource == null)
            return null;

        var text = await LocalizationLoader.ExportAsync(resource);

        return string.IsNullOrEmpty(text)
            ? null
            : new PlainTextFilePreviewViewModel { Data = text };
    }
}