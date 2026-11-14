using BG3PakViewer.VirtualTextures.Models;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Extracts the layers of an indexed virtual texture into DDS streams.
/// </summary>
/// <remarks>
///     Extraction is separate from the index: it parses the tile set of the requested texture and opens
///     the page files the layer needs, so it depends on the loaded package rather than on the index. The
///     parsed tile set outlives the extraction that read it, which is why <see cref="ReleaseAsync" /> exists.
///     Releasing the service waits for an extraction in progress, so it is done through
///     <see cref="IAsyncDisposable.DisposeAsync" /> rather than a blocking disposal.
/// </remarks>
public interface IVirtualTextureExtractor : IAsyncDisposable
{
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

    /// <summary>
    ///     Tells whether a layer of a texture holds any tile, without extracting it.
    /// </summary>
    /// <param name="entry">The texture to inspect.</param>
    /// <param name="layer">The layer index.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true" /> when the layer holds data.</returns>
    Task<bool> HasLayerDataAsync(VirtualTextureEntry entry, int layer, CancellationToken cancellationToken);

    /// <summary>
    ///     Releases the parsed tile set, so the package it was read from can be replaced or closed, while
    ///     keeping the extractor usable for the next package.
    /// </summary>
    /// <returns>A task that completes when the tile set is released.</returns>
    Task ReleaseAsync();
}
