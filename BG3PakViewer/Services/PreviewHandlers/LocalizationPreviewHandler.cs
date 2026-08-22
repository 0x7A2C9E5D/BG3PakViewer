using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Loader;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

internal class LocalizationPreviewHandler : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsLocalizationFormat(fileExtension);
    }

    public async Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension)
    {
        var resource = await LocalizationLoader.LoadAsync(stream);
        if (resource == null) return null;

        // Build the table rows off the UI thread so large localizations don't
        // block the UI.
        return await Task.Run(() => LocalizationPreviewViewModel.FromResource(resource));
    }
}