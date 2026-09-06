namespace BG3PakViewer.Utils;

/// <summary>
///     TextOperations
/// </summary>
public static class TextOperations
{
    /// <summary>
    ///     Truncates the text to the specified number of lines.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="maxLines"></param>
    /// <returns></returns>
    public static string TruncateToLines(string text, int maxLines)
    {
        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
        return lines.Length <= maxLines ? text : string.Join(Environment.NewLine, lines.Take(maxLines));
    }
}