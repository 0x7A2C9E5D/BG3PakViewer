namespace BG3PakViewer.VirtualTextures.Models;

/// <summary>
///     One texture of a virtual texture tile set, as listed by the catalog.
/// </summary>
/// <param name="path">The package path of the tile set (.gts) the texture belongs to.</param>
/// <param name="name">The texture name, without extension.</param>
/// <param name="layerCount">The number of layers of the tile set.</param>
/// <param name="index">The index of the texture in the tile set's texture list.</param>
public sealed record VirtualTextureEntry(
    string path,
    string name,
    int layerCount,
    int index);
