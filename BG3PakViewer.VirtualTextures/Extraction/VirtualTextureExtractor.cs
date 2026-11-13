using BG3PakViewer.VirtualTextures.Models;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Extracts the layers of the indexed virtual textures from the loaded package.
/// </summary>
/// <remarks>
///     Extraction is serialized because one extraction reads the package at a time. Each call parses the
///     tile set it needs, opens only the page file it is reading, and releases both when it ends, so
///     nothing opened from the package outlives the call that opened it.
/// </remarks>
/// <param name="fileSource">The files of the loaded package the tile sets and their page files are read from.</param>
public sealed class VirtualTextureExtractor(VirtualTextureFileSource fileSource) : IVirtualTextureExtractor
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    ///     Extracts one layer of a texture into a DDS stream, reporting progress in percent.
    /// </summary>
    /// <remarks>
    ///     The tile set is parsed for this call and released when it ends, together with the page file
    ///     the extraction read.
    /// </remarks>
    /// <param name="entry">The texture to extract.</param>
    /// <param name="layer">The layer index.</param>
    /// <param name="progress">Reports the extraction progress, in percent.</param>
    /// <param name="cancellationToken">Cancels the extraction.</param>
    /// <returns>The layer data, or <see langword="null" /> when the layer contains no data.</returns>
    public async Task<Stream?> ExtractLayerAsync(VirtualTextureEntry entry, int layer, IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var file = await Task.Run(() => new VirtualTileSetFile(fileSource.Open(entry.GtsPath)),
                    CancellationToken.None)
                .ConfigureAwait(false);
            var layerExtractor = new VirtualTextureLayerExtractor(file,
                pageFileName => fileSource.Open(
                    VirtualTexturePackagePath.GetPageFilePath(entry.GtsPath, pageFileName)));
            return await layerExtractor.ExtractAsync(entry.Index, layer, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}