namespace BG3PakViewer.Dialogs.Models;

/// <summary>
///     One texture of a virtual texture tile set, as listed by the catalog.
/// </summary>
/// <param name="GtsPath">The package path of the tile set (.gts) the texture belongs to.</param>
/// <param name="Name">The texture name, without extension.</param>
/// <param name="Width">The width of the texture, in pixels.</param>
/// <param name="Height">The height of the texture, in pixels.</param>
/// <param name="LayerCount">The number of layers of the tile set.</param>
/// <param name="Index">The index of the texture in the tile set's texture list.</param>
public sealed record VirtualTextureEntry(
    string GtsPath,
    string Name,
    int Width,
    int Height,
    int LayerCount,
    int Index);
