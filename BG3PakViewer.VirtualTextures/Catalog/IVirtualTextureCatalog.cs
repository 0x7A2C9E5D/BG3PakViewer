using BG3PakViewer.VirtualTextures.Models;

namespace BG3PakViewer.VirtualTextures.Catalog;

/// <summary>
///     The virtual textures of the loaded package, indexed across every tile set (.gts) it contains.
/// </summary>
/// <remarks>
///     Indexing reads the tile set metadata only, so it stays cheap: it does not open any GTP page
///     file. The layers of the indexed textures are produced by <see cref="IVirtualTextureExtractor" />.
/// </remarks>
public interface IVirtualTextureCatalog : IDisposable
{
    /// <summary>
    ///     Whether the textures of the loaded package have been indexed.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    ///     The indexed textures, in package order; empty until <see cref="LoadAsync" /> completes.
    /// </summary>
    IReadOnlyList<VirtualTextureEntry> Entries { get; }

    /// <summary>
    ///     Indexes the tile sets of the loaded package.
    /// </summary>
    /// <param name="progress">Reports how many tile sets have been indexed.</param>
    Task LoadAsync(IProgress<VirtualTextureIndexProgress>? progress);

    /// <summary>
    ///     Stops indexing and releases the index, so the package can be closed afterward.
    /// </summary>
    Task ClearAsync();
}