using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Loader;
using BG3PakViewer.Utils;
using Cysharp.Text;

namespace BG3PakViewer.Services.PreviewHandlers;

public class LarianResourcePreviewHandler(IAppSettings appSettings) : IPreviewHandler{
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

        if (string.IsNullOrEmpty(text))
            return null;

        var truncated = await TruncateTextToLines(text, appSettings.MaxPreviewLines);

        return new PlainTextFilePreviewViewModel { Data = truncated };
    }

    private static async Task<string> TruncateTextToLines(string text, int maxLines)
    {
        return await Task.Run(() =>
        {
            var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None).Take(maxLines);
            using var stringBuilder = ZString.CreateStringBuilder();
            foreach (var line in lines) stringBuilder.AppendLine(line);
            return stringBuilder.ToString();
        });
    }
}