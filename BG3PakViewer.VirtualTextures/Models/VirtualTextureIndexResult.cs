namespace BG3PakViewer.VirtualTextures.Models;

/// <summary>
///     The textures of the loaded package and the tile sets that could not be read while indexing.
///     Both collections keep package and tile set order. The catalog builds them as mutable lists and
///     wraps them in read-only views once indexing ends, so the cached result can be shared by every
///     caller without being copied again. The <see cref="IReadOnlyList{T}" /> interface alone does
///     not enforce immutability; that guarantee comes from the catalog.
/// </summary>
/// <param name="entries">Every indexed texture, in package and tile set order.</param>
/// <param name="failures">The tile sets whose metadata could not be parsed, in package order.</param>
public sealed record VirtualTextureIndexResult(
    IReadOnlyList<VirtualTextureEntry> entries,
    IReadOnlyList<VirtualTextureIndexFailure> failures);
