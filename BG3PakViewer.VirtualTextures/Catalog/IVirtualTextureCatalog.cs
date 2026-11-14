using BG3PakViewer.VirtualTextures.Extraction;
using BG3PakViewer.VirtualTextures.Models;

namespace BG3PakViewer.VirtualTextures.Catalog;

/// <summary>
///     The virtual textures of the loaded package, streamed from every tile set (.gts) it contains.
/// </summary>
/// <remarks>
///     Indexing reads tile set metadata only, so it stays cheap: it does not open any GTP page file. The
///     layers of the indexed textures are produced by <see cref="IVirtualTextureExtractor" />. The
///     enumeration is pull-driven: each tile set is parsed on a worker as the caller asks for its
///     textures, and only one tile set is held in memory at a time. The index is not cached, so every
///     call reads the tile sets again and the package has to stay loaded until the enumeration ends.
///     Abandoning or cancelling the enumeration stops the read without separate cleanup;
///     <see cref="IAsyncDisposable.DisposeAsync" /> only marks the catalog as disposed.
/// </remarks>
public interface IVirtualTextureCatalog : IAsyncDisposable
{
    /// <summary>
    ///     Streams every texture of the package, in package and tile set order, one tile set at a time.
    /// </summary>
    /// <param name="progress">Reports how many tile sets have been indexed.</param>
    /// <param name="failures">
    ///     Pass an empty collection: tile sets that could not be read are added to it during the
    ///     enumeration. Read it after the enumeration ends; abandoning it may leave the list incomplete.
    /// </param>
    /// <param name="cancellationToken">Stops indexing and ends the stream.</param>
    /// <returns>The textures, produced as each tile set is parsed.</returns>
    IAsyncEnumerable<VirtualTextureEntry> LoadAsync(
        IProgress<VirtualTextureIndexProgress>? progress,
        ICollection<VirtualTextureTileSetFailure> failures,
        CancellationToken cancellationToken = default);
}
