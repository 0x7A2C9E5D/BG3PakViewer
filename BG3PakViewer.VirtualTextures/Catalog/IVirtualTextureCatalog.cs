using BG3PakViewer.VirtualTextures.Models;

namespace BG3PakViewer.VirtualTextures.Catalog;

/// <summary>
///     The virtual textures of the loaded package, indexed across every tile set (.gts) it contains.
/// </summary>
/// <remarks>
///     Indexing reads the tile set metadata only, so it stays cheap: it does not open any GTP page
///     file. Extracting a layer parses the tile set it belongs to, opens the pages that layer needs and
///     releases both when the call ends, so nothing opened from the package outlives the extraction.
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
    ///     Stops indexing, releases the index and every stream opened from the package, so the
    ///     package can be closed afterward.
    /// </summary>
    Task ClearAsync();

    /// <summary>
    ///     Extracts one layer of a texture into a DDS stream, reporting progress in percent.
    /// </summary>
    /// <param name="entry">The texture to extract.</param>
    /// <param name="layer">The layer index.</param>
    /// <param name="progress">Reports the extraction progress, in percent.</param>
    /// <param name="cancellationToken">Cancels the extraction.</param>
    /// <returns>The layer data, or <see langword="null" /> when the layer contains no data.</returns>
    Task<Stream?> ExtractLayerAsync(VirtualTextureEntry entry, int layer, IProgress<double>? progress,
        CancellationToken cancellationToken);
}