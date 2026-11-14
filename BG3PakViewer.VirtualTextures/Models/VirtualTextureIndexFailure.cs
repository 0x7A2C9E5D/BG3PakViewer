namespace BG3PakViewer.VirtualTextures.Models;

/// <summary>
///     A tile set the catalog could not index, so its textures are missing from the index.
/// </summary>
/// <param name="path">The package path of the tile set (.gts).</param>
/// <param name="Reason">Why the tile set could not be read.</param>
public sealed record VirtualTextureIndexFailure(string path, string Reason);
