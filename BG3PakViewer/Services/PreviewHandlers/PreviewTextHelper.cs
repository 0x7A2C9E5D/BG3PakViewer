using Cysharp.Text;

namespace BG3PakViewer.Services.PreviewHandlers;

public static  class PreviewTextHelper
{
    public static async Task<string> TruncateTextToLines(string text, int maxLines)
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