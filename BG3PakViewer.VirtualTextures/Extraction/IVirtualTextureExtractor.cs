using BG3PakViewer.VirtualTextures.Models;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Extracts the layers of an indexed virtual texture into DDS streams.
/// </summary>
/// <remarks>
///     Extraction is separate from the index: it parses the tile set of the requested texture and opens
///     the page files the layer needs, so it depends on the loaded package rather than on the index. The
///     parsed tile set outlives the extraction that read it, which is why <see cref="ClearAsync" /> exists.
/// </remarks>
public interface IVirtualTextureExtractor : IDisposable
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
    ///     Releases the tile set the extractor keeps parsed, so the package it was read from can be closed.
    /// </summary>
    /// <returns>A task that completes when the tile set is released.</returns>
    Task ClearAsync();
}
