using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

public class TextPreviewHandler(IAppSettings appSettings) : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsPlainText(fileExtension);
    }

    public async Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension)
    {
        using var reader = new StreamReader(stream, false);
        var text = await reader.ReadToEndAsync();
        var truncated = await PreviewTextHelper.TruncateTextToLines(text, appSettings.MaxPreviewLines);

        return new PlainTextFilePreviewViewModel
        {
            Data = truncated
        };
    }
}