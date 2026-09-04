namespace BG3PakViewer.Messaging;

/// <summary>
///     Broadcasts a search query from the search boxes (tree / global title-bar box) to the
///     active preview. An empty or null <see cref="Text" /> signals that the search should
///     be cleared so the full content is shown again.
/// </summary>
public sealed class SearchMessage(string? text)
{
    /// <summary>
    ///     Gets the search text.
    /// </summary>
    public string? Text { get; } = text;
}