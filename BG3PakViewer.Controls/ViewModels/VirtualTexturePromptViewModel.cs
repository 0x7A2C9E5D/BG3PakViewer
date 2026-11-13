using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     Tells the user that virtual texture files are viewed and exported from the virtual texture
///     preview export tool.
/// </summary>
/// <param name="textureName">
///     The texture name embedded in a page file name, or <see langword="null" /> when the file does
///     not name a single texture; the prompt still opens the tool on the full list then.
/// </param>
public sealed class VirtualTexturePromptViewModel(string? textureName) : ObservableObject
{
    /// <summary>
    ///     The texture name embedded in the page file name, if any.
    /// </summary>
    public string? TextureName { get; } = textureName;
}