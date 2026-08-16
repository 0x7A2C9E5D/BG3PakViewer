using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using Cysharp.Text;

namespace BG3PakViewer.Services.PreviewHandlers;

internal abstract class TextBasedPreviewHandler(IAppSettings appSettings) : IPreviewHandler
{
    public abstract bool CanHandle(string fileExtension);

    public async Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension)
    {
        var text = await GetTextAsync(stream, fileExtension);

        if (string.IsNullOrEmpty(text))
            return null;

        var truncated = await TruncateTextToLines(text, appSettings.MaxPreviewLines);

        return new PlainTextFilePreviewViewModel { Data = truncated };
    }

    protected abstract Task<string?> GetTextAsync(Stream stream, string fileExtension);

    private static async Task<string> TruncateTextToLines(string text, int maxLines)
    {
        return await Task.Run(() =>
        {
            var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
            if (lines.Length <= maxLines) return text;
            lines = [.. lines.Take(maxLines)];
            using var stringBuilder = ZString.CreateStringBuilder();
            foreach (var line in lines) stringBuilder.AppendLine(line);
            return stringBuilder.ToString();
        });
    }
}