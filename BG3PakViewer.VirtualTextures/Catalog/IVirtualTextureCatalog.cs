using BG3PakViewer.VirtualTextures.Extraction;
using BG3PakViewer.VirtualTextures.Models;

namespace BG3PakViewer.VirtualTextures.Catalog;

/// <summary>
///     The virtual textures of the loaded package, indexed from every tile set (.gts) it contains.
/// </summary>
/// <remarks>
///     Indexing reads tile set metadata only, so it stays cheap: it does not open any GTP page file. The
///     layers of the indexed textures are produced by <see cref="IVirtualTextureExtractor" />. Tile sets
///     are parsed one at a time on a worker, a transient read failure is retried once, and the ordered
///     index is returned when the read ends. The catalog caches a complete read-only index for the
///     loaded package and rebuilds it when the package changes; an index that still has unreadable tile
///     sets is not cached, so the next call retries them instead of returning the same missing textures.
///     Calls made while an index is being built wait for it instead of reading the tile sets twice.
///     A cache hit does not touch the package at all.
/// </remarks>
public interface IVirtualTextureCatalog
{
    /// <summary>
    ///     Returns every virtual texture of the loaded package, indexing its tile sets only when the
    ///     cached index is missing or stale.
    /// </summary>
    /// <param name="progress">
    ///     Reports how many tile sets have been indexed; a cache hit reports the final count once.
    /// </param>
    /// <param name="cancellation">Stops indexing, and also cancels an otherwise immediate cache hit.</param>
    /// <returns>
    ///     Every texture in package and tile set order, plus the tile sets that could not be read, as
    ///     read-only collections shared with every other caller.
    /// </returns>
    Task<VirtualTextureIndexResult> IndexAsync(
        IProgress<VirtualTextureIndexProgress>? progress,
        CancellationToken cancellation = default);
}
