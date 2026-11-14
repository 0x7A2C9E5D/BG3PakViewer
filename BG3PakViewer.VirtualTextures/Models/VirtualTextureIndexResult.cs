namespace BG3PakViewer.VirtualTextures.Models;

/// <summary>
///     The textures of the loaded package and the tile sets that could not be read while indexing.
/// </summary>
/// <param name="entries">Every indexed texture, in package and tile set order.</param>
/// <param name="failures">The tile sets whose metadata could not be parsed.</param>
public sealed record VirtualTextureIndexResult(
    IReadOnlyList<VirtualTextureEntry> entries,
    IReadOnlyList<VirtualTextureIndexFailure> failures);
