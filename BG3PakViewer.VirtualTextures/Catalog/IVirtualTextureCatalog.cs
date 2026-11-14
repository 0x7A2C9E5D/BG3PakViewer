using BG3PakViewer.VirtualTextures.Extraction;
using BG3PakViewer.VirtualTextures.Models;

namespace BG3PakViewer.VirtualTextures.Catalog;

/// <summary>
///     The virtual textures of the loaded package, indexed across every tile set (.gts) it contains.
/// </summary>
/// <remarks>
///     Indexing reads the tile set metadata only, so it stays cheap: it does not open any GTP page
///     file. The layers of the indexed textures are produced by <see cref="IVirtualTextureExtractor" />.
///     Releasing the catalog waits for a running index to stop reading the package, so it is done
///     through <see cref="IAsyncDisposable.DisposeAsync" /> instead of a blocking disposal.
/// </remarks>
public interface IVirtualTextureCatalog : IAsyncDisposable
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
    ///     The tile sets that could not be indexed, in package order; empty when every tile set was read.
    /// </summary>
    /// <remarks>
    ///     A tile set that fails to parse is not the same as one that holds no textures: without this the
    ///     two look identical, because both contribute no entries.
    /// </remarks>
    IReadOnlyList<VirtualTextureTileSetFailure> FailedTileSets { get; }

    /// <summary>
    ///     Indexes the tile sets of the loaded package.
    /// </summary>
    /// <param name="progress">Reports how many tile sets have been indexed.</param>
    /// <returns>A task that completes when the index is built.</returns>
    Task LoadAsync(IProgress<VirtualTextureIndexProgress>? progress);

    /// <summary>
    ///     Stops indexing and releases the index, so the package can be closed afterward.
    /// </summary>
    /// <returns>A task that completes when the index has been released.</returns>
    Task ClearAsync();
}