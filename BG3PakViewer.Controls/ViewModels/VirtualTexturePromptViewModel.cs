using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     Tells the user that virtual texture files are viewed and exported from the virtual texture
///     preview export tool.
/// </summary>
/// <param name="initialSearch">
///     What the tool searches for: the texture name embedded in a page file name, or the file name of
///     a tile set; <see langword="null" /> when the file names neither, and the tool opens on the full
///     list then.
/// </param>
public sealed class VirtualTexturePromptViewModel(string? initialSearch) : ObservableObject
{
    /// <summary>
    ///     What the tool searches for, or <see langword="null" /> when it opens on the full list.
    /// </summary>
    public string? InitialSearch { get; } = initialSearch;
}