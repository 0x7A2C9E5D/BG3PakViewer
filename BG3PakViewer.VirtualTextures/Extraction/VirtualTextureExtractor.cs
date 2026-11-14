using BG3PakViewer.Contracts;
using BG3PakViewer.VirtualTextures.Models;
using BG3PakViewer.VirtualTextures.TileSets;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Extracts the layers of the indexed virtual textures from the loaded package.
/// </summary>
/// <remarks>
///     Extraction is serialized because one extraction reads the package at a time. The extractor of the
///     last tile set is kept, because every layer of every texture of a tile set needs exactly that tile
///     set; only the last one is kept, so the memory this costs stays bounded by one tile set and the
///     handle it holds is released as soon as another tile set is needed. <see cref="Release" />
///     releases it when the package it was read from is replaced.
/// </remarks>
/// <param name="packageService">The loaded package the tile sets and their page files are read from.</param>
public sealed class VirtualTextureExtractor(IPackageService packageService) : IVirtualTextureExtractor
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;
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
    ///     Tells whether a layer of a texture holds any tile, without extracting it.
    /// </summary>
    /// <param name="entry">The texture to inspect.</param>
    /// <param name="layer">The layer index.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true" /> when the layer holds data.</returns>
    public async Task<bool> HasLayerDataAsync(VirtualTextureEntry entry, int layer,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var extractor = await OpenExtractorAsync(entry.GtsPath).ConfigureAwait(false);
            return await Task.Run(() => extractor.HasLayerData(entry.Index, layer), cancellationToken)
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
    public async Task ReleaseAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_cachedExtractor is not null)
                await _cachedExtractor.DisposeAsync().ConfigureAwait(false);
            _cachedExtractorPath = null;
            _cachedExtractor = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    ///     Waits for an extraction in progress, releases the parsed tile set and marks the service as
    ///     disposed. ReleaseAsync does the same cleanup when a package is replaced, but keeps the service usable.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await ReleaseAsync().ConfigureAwait(false);
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

        // Forget the cached tile set before the new one is parsed. If parsing throws, an empty cache
        // lets the next call try again; keeping the extractor that was just disposed would make every
        // later call for the old path hand out a released object for the rest of the session.
        if (_cachedExtractor is not null)
            await _cachedExtractor.DisposeAsync().ConfigureAwait(false);
        _cachedExtractor = null;
        _cachedExtractorPath = null;
        var file = await Task.Run(() => new VirtualTextureTileSetFile(OpenPackageFile(gtsPath)), CancellationToken.None)
            .ConfigureAwait(false);
        _cachedExtractor = new VirtualTextureLayerExtractor(file,
            pageFileName => OpenPackageFile(GetPageFilePath(gtsPath, pageFileName)));
        _cachedExtractorPath = gtsPath;
        return _cachedExtractor;
    }

    /// <summary>
    ///     Composes the package path of a page file, which sits next to its tile set.
    /// </summary>
    /// <param name="gtsPath">The package path of the tile set the page file sits next to.</param>
    /// <param name="pageFileName">The name of the page file, as the tile set records it.</param>
    /// <returns>The package path of the page file.</returns>
    private static string GetPageFilePath(string gtsPath, string pageFileName)
    {
        var directory = Path.GetDirectoryName(gtsPath)?.Replace('\\', '/');
        return string.IsNullOrEmpty(directory) ? pageFileName : $"{directory}/{pageFileName}";
    }

    /// <summary>
    ///     Opens one file of the package.
    /// </summary>
    /// <param name="packagePath">The package path of the file.</param>
    /// <returns>The content of the file.</returns>
    /// <exception cref="FileNotFoundException">The package does not contain the file.</exception>
    private async Task<Stream> OpenPackageFile(string packagePath)
    {
        return (await packageService.GetFileByPath(packagePath).ConfigureAwait(false))?.CreateContentReader()
               ?? throw new FileNotFoundException($"Package file not found: {packagePath}");
    }
}
