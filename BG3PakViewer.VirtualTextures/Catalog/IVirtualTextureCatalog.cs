using BG3PakViewer.VirtualTextures.Extraction;
using BG3PakViewer.VirtualTextures.Models;

namespace BG3PakViewer.VirtualTextures.Catalog;

/// <summary>
///     The virtual textures of the loaded package, indexed from every tile set (.gts) it contains.
/// </summary>
/// <remarks>
///     Indexing reads tile set metadata only, so it stays cheap: it does not open any GTP page file. The
///     layers of the indexed textures are produced by <see cref="IVirtualTextureExtractor" />. Tile sets
///     are parsed one at a time on a worker and the complete index is returned when the read ends. The
///     catalog keeps no state, so every call reads the tile sets again.
/// </remarks>
public interface IVirtualTextureCatalog
{
    /// <summary>
    ///     Indexes every tile set of the loaded package, in package and tile set order.
    /// </summary>
    /// <param name="progress">Reports how many tile sets have been indexed.</param>
    /// <param name="cancellation">Stops indexing.</param>
    /// <returns>Every texture, plus the tile sets that could not be read.</returns>
    Task<VirtualTextureIndexResult> IndexAsync(
        IProgress<VirtualTextureIndexProgress>? progress,
        CancellationToken cancellation = default);
}
