namespace BG3PakViewer.VirtualTextures.Models;

/// <summary>
///     One texture of a virtual texture tile set, as listed by the catalog.
/// </summary>
/// <param name="GtsPath">The package path of the tile set (.gts) the texture belongs to.</param>
/// <param name="Name">The texture name, without extension.</param>
/// <param name="LayerCount">The number of layers of the tile set.</param>
/// <param name="Index">The index of the texture in the tile set's texture list.</param>
public sealed record VirtualTextureEntry(
    string GtsPath,
    string Name,
    int LayerCount,
    int Index);