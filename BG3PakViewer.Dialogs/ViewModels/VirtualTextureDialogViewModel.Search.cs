using CommunityToolkit.Mvvm.Input;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     Filtering the indexed list by texture name or tile set file, and the search box commands that
///     submit or clear the query.
/// </summary>
public partial class VirtualTextureDialogViewModel
{
    /// <summary>
    ///     The texture name or tile set file name the list was opened with, if any.
    /// </summary>
    public string? InitialSearch { get; }

    /// <summary>
    ///     Builds the filter the list view applies: a case-insensitive substring match on the texture
    ///     name or on the tile set file the texture comes from.
    /// </summary>
    /// <param name="searchText">The submitted search text.</param>
    /// <returns>The predicate, or <see langword="null" /> when everything is shown.</returns>
    protected override Predicate<object>? BuildFilter(string? searchText)
    {
        return string.IsNullOrWhiteSpace(searchText)
            ? null
            : item => item is VirtualTextureItemViewModel texture && Matches(texture, searchText);
    }

    /// <summary>
    ///     Whether a texture matches the search text, by texture name or by the tile set file it comes from,
    ///     so searching a tile set name narrows the list to the textures of that tile set.
    /// </summary>
    /// <param name="texture">The texture to test.</param>
    /// <param name="searchText">The submitted search text.</param>
    /// <returns><see langword="true" /> when the texture matches.</returns>
    private static bool Matches(VirtualTextureItemViewModel texture, string searchText)
    {
        return texture.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               texture.Source.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Applies the text the search box submits, with Enter or with the query icon.
    /// </summary>
    /// <param name="query">The text typed in the search box.</param>
    [RelayCommand]
    private void Search(string? query)
    {
        SearchText = query ?? string.Empty;
    }

    /// <summary>
    ///     Clears the filter as soon as the search box is emptied, without waiting for a submit.
    /// </summary>
    /// <param name="query">The text the search box now holds.</param>
    [RelayCommand]
    private void ClearSearch(string? query)
    {
        if (!string.IsNullOrWhiteSpace(query)) return;
        SearchText = string.Empty;
    }
}
