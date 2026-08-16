using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

internal abstract class TextBasedPreviewHandler(IAppSettings appSettings) : IPreviewHandler
{
    public abstract bool CanHandle(string fileExtension);

    public async Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension)
    {
        var text = await GetTextAsync(stream, fileExtension);

        if (string.IsNullOrEmpty(text))
            return null;

        var truncated = await TextOperations.TruncateToLinesAsync(text, appSettings.MaxPreviewLines);

        return new PlainTextFilePreviewViewModel { Data = truncated };
    }

    protected abstract Task<string?> GetTextAsync(Stream stream, string fileExtension);
}