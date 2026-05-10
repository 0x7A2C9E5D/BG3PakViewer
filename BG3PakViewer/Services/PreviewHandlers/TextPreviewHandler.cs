using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Utils;
using Cysharp.Text;

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
        var truncated = await TruncateTextToLines(text, appSettings.MaxPreviewLines);

        return new PlainTextFilePreviewViewModel
        {
            Data = truncated
        };
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