using BG3PakViewer.Contracts;
using BG3PakViewer.VirtualTextures.Models;
using BG3PakViewer.VirtualTextures.TileSets;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Extracts the layers of indexed virtual textures from the loaded package, one tile set at a time.
/// </summary>
/// <remarks>
///     All operations, including layer extraction, the layer-data check and cache release, are serialized
///     by a gate because the parsed tile set and the package it came from are not safe for concurrent use.
///     Only the most recently used tile set is parsed, and a failed parse leaves the cache empty so the
///     next call can retry. GTP page files are never cached: an extraction opens the one a band is reading
///     and closes it before returning. The returned DDS stream belongs to the caller, which must dispose
///     it. The cached tile set survives closing the toolbox and is released when another tile set is
///     needed or when <see cref="DisposeAsync" /> runs at shutdown.
/// </remarks>
public sealed partial class VirtualTextureExtractor : IVirtualTextureExtractor
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IPackageService _packageService;
    private bool _disposed;
    private VirtualTextureTileSetFile? _tileSet;
    private string? _tileSetPath;

    /// <summary>
    ///     Creates the extractor for the loaded package.
    /// </summary>
    /// <param name="packageService">The loaded package the tile sets and their page files are read from.</param>
    public VirtualTextureExtractor(IPackageService packageService)
    {
        _packageService = packageService;
    }

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
            await EnsureCachedTileSetAsync(entry.path).ConfigureAwait(false);
            var tileProgress = CreateProgress(progress);
            return await Task.Run(() => TranscodeLayer(entry, layer, tileProgress, cancellationToken), cancellationToken)
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
    public async Task<bool> HasLayerAsync(VirtualTextureEntry entry, int layer,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureCachedTileSetAsync(entry.path).ConfigureAwait(false);
            return await Task.Run(() => FindLevel(layer, _tileSet!.Textures[entry.index]) is not null,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    ///     Waits for an extraction in progress, releases the parsed tile set and marks the service as
    ///     disposed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_disposed) return;
            await _gate.WaitAsync().ConfigureAwait(false);
            await ReleaseCachedTileSetAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
            _disposed = true;
        }
    }

    /// <summary>
    ///     Parses the tile set at <paramref name="path" /> when the cache holds a different one. The
    ///     caller must hold <see cref="_gate" />; on return <see cref="_tileSet" /> and
    ///     <see cref="_tileSetPath" /> both describe that tile set.
    /// </summary>
    /// <param name="path">The package path of the tile set.</param>
    private async Task EnsureCachedTileSetAsync(string path)
    {
        if (_tileSet is not null && _tileSetPath == path) return;

        // Forget the cached tile set before the new one is parsed. If parsing throws, an empty cache
        // lets the next call try again; keeping the tile set that was just disposed would make every
        // later call for the old path hand out a released object for the rest of the session.
        await ReleaseCachedTileSetAsync().ConfigureAwait(false);
        var stream = await OpenPackageFileAsync(path).ConfigureAwait(false);
        _tileSet = await Task.Run(() => new VirtualTextureTileSetFile(stream), CancellationToken.None)
            .ConfigureAwait(false);
        _tileSetPath = path;
    }

    /// <summary>
    ///     Releases the cached tile set. The caller must hold <see cref="_gate" />.
    /// </summary>
    /// <returns>A task that completes when the tile set is released.</returns>
    private async Task ReleaseCachedTileSetAsync()
    {
        if (_tileSet is not null)
            await _tileSet.DisposeAsync().ConfigureAwait(false);
        _tileSet = null;
        _tileSetPath = null;
    }

    /// <summary>
    ///     Opens the GTP page file with the given name, which sits next to the cached tile set.
    /// </summary>
    /// <param name="fileName">The name of the page file, as the tile set records it.</param>
    /// <returns>The content of the page file.</returns>
    private Task<Stream> OpenPageFileAsync(string fileName)
    {
        var directory = Path.GetDirectoryName(_tileSetPath)?.Replace('\\', '/');
        var path = string.IsNullOrEmpty(directory) ? fileName : $"{directory}/{fileName}";
        return OpenPackageFileAsync(path);
    }

    /// <summary>
    ///     Opens one file of the package.
    /// </summary>
    /// <param name="path">The package path of the file.</param>
    /// <returns>The content of the file.</returns>
    /// <exception cref="FileNotFoundException">The package does not contain the file.</exception>
    private async Task<Stream> OpenPackageFileAsync(string path)
    {
        return (await _packageService.GetFileByPath(path).ConfigureAwait(false))?.CreateContentReader()
               ?? throw new FileNotFoundException($"Package file not found: {path}");
    }
}
