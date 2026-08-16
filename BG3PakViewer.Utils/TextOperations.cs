using Cysharp.Text;

namespace BG3PakViewer.Utils;

public static class TextOperations
{
    public static async Task<string> TruncateToLinesAsync(string text, int maxLines)
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