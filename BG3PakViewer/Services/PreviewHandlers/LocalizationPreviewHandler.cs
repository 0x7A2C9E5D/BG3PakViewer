using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Loader;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

public class LocalizationPreviewHandler(IAppSettings appSettings) : IPreviewHandler
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

        if (string.IsNullOrEmpty(text))
            return null;

        var truncated = await PreviewTextHelper.TruncateTextToLines(text, appSettings.MaxPreviewLines);

        return new PlainTextFilePreviewViewModel { Data = truncated };
    }
}