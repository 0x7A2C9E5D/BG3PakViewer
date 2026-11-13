using BG3PakViewer.Contracts;
using BG3PakViewer.VirtualTextures.Models;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Extracts the layers of the indexed virtual textures from the loaded package.
/// </summary>
/// <remarks>
///     Extraction is serialized because one extraction reads the package at a time. The extractor of the
///     last tile set is kept, because every layer of every texture of a tile set needs exactly that tile
///     set; only the last one is kept, so the memory this costs stays bounded by one tile set and the
///     handle it holds is released as soon as another tile set is needed. <see cref="ClearAsync" />
///     releases it when the package it was read from is replaced.
/// </remarks>
/// <param name="packageService">The loaded package the tile sets and their page files are read from.</param>
public sealed class VirtualTextureExtractor(IPackageService packageService) : IVirtualTextureExtractor
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private VirtualTextureLayerExtractor? _cachedExtractor;
    private string? _cachedExtractorPath;

    /// <summary>
    ///     Extracts one layer of a texture into a DDS stream, reporting progress in percent.
    /// </summary>
    /// <remarks>
    ///     Only the page file the extraction reads is opened for this call and released when it ends; the
    ///     tile set stays parsed for the next extraction.
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
            var extractor = await OpenExtractorAsync(entry.GtsPath).ConfigureAwait(false);
            return await extractor.ExtractAsync(entry.Index, layer, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    ///     Releases the parsed tile set, so the package it was read from can be closed.
    /// </summary>
    /// <returns>A task that completes when the tile set is released.</returns>
    public async Task ClearAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ReleaseCachedExtractor();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    ///     Releases the parsed tile set.
    /// </summary>
    public void Dispose()
    {
        ReleaseCachedExtractor();
    }

    /// <summary>
    ///     Gets the extractor of the tile set at <paramref name="gtsPath" />, parsing that tile set when the
    ///     last extraction used a different one.
    /// </summary>
    /// <param name="gtsPath">The package path of the tile set.</param>
    /// <returns>The extractor of that tile set.</returns>
    private async Task<VirtualTextureLayerExtractor> OpenExtractorAsync(string gtsPath)
    {
        if (_cachedExtractor is not null && _cachedExtractorPath == gtsPath) return _cachedExtractor;

        ReleaseCachedExtractor();
        var file = await Task.Run(() => new VirtualTileSetFile(OpenPackageFile(gtsPath)), CancellationToken.None)
            .ConfigureAwait(false);
        _cachedExtractor = new VirtualTextureLayerExtractor(file,
            pageFileName => OpenPackageFile(VirtualTexturePackagePath.GetPageFilePath(gtsPath, pageFileName)));
        _cachedExtractorPath = gtsPath;
        return _cachedExtractor;
    }

    /// <summary>
    ///     Releases the cached extractor and the tile set it owns.
    /// </summary>
    private void ReleaseCachedExtractor()
    {
        var cached = _cachedExtractor;
        _cachedExtractor = null;
        _cachedExtractorPath = null;
        cached?.Dispose();
    }

    /// <summary>
    ///     Opens one file of the package.
    /// </summary>
    /// <param name="packagePath">The package path of the file.</param>
    /// <returns>The content of the file.</returns>
    /// <exception cref="FileNotFoundException">The package does not contain the file.</exception>
    private Stream OpenPackageFile(string packagePath)
    {
        return packageService.GetFileByPath(packagePath)?.CreateContentReader()
               ?? throw new FileNotFoundException($"Package file not found: {packagePath}");
    }
}