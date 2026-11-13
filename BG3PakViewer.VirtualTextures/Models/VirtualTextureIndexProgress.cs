namespace BG3PakViewer.VirtualTextures.Models;

/// <summary>
///     How far the virtual texture catalog has got while indexing the tile sets of a package.
/// </summary>
/// <param name="Indexed">The number of tile sets that have been indexed.</param>
/// <param name="Total">The number of tile sets that are going to be indexed.</param>
public sealed record VirtualTextureIndexProgress(int Indexed, int Total);