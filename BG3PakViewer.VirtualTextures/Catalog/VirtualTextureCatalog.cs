using System.Diagnostics;
using BG3PakViewer.Contracts;
using BG3PakViewer.Utils;
using BG3PakViewer.VirtualTextures.Extraction;
using BG3PakViewer.VirtualTextures.Models;
using Serilog;

namespace BG3PakViewer.VirtualTextures.Catalog;

/// <summary>
///     Indexes every virtual texture tile set (.gts) of the loaded package and extracts layers from them.
/// </summary>
/// <remarks>
///     The index holds metadata only, so it does not open any GTP page file. Extraction is serialized
///     because one extraction reads the package at a time; each one parses the tile set it needs, opens
///     only the page file it is reading, and releases both when it ends, so nothing opened from the
///     package outlives the call that opened it. <see cref="ClearAsync" /> waits for the work in
///     progress before releasing the index, which is what lets the package be closed safely.
/// </remarks>
/// <param name="packageService">The loaded package the tile sets are read from.</param>
public sealed class VirtualTextureCatalog(IPackageService packageService) : IVirtualTextureCatalog
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;
    private List<VirtualTextureEntry> _entries = [];
    private CancellationTokenSource? _loadCancellation;
    private Task? _loadTask;

    /// <summary>
    ///     Whether the textures of the loaded package have been indexed.
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    ///     The indexed textures, in package order; empty until <see cref="LoadAsync" /> completes.
    /// </summary>
    public IReadOnlyList<VirtualTextureEntry> Entries => _entries;

    /// <summary>
    ///     Indexes the tile sets of the loaded package.
    /// </summary>
    /// <param name="progress">Reports how many tile sets have been indexed.</param>
    public Task LoadAsync(IProgress<VirtualTextureIndexProgress>? progress)
    {
        if (IsLoaded) return Task.CompletedTask;
        _loadCancellation ??= new CancellationTokenSource();
        _loadTask ??= IndexAsync(progress, _loadCancellation.Token);
        return _loadTask;
    }

    /// <summary>
    ///     Stops indexing, releases the index and every stream opened from the package, so the
    ///     package can be closed afterward.
    /// </summary>
    public async Task ClearAsync()
    {
        if (_loadCancellation is not null) await _loadCancellation.CancelAsync();
        if (_loadTask is not null)
            try
            {
                await _loadTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The index was stopped on purpose; the entries it did not reach are not needed.
            }

        // Clearing cannot be canceled: the package is about to be replaced either way.
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            _entries = [];
            IsLoaded = false;
            _loadCancellation?.Dispose();
            _loadCancellation = null;
            _loadTask = null;
        }
        finally
        {
            _gate.Release();
        }
    }

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
            using var handle = await Task.Run(() => CreateHandle(entry.GtsPath), CancellationToken.None)
                .ConfigureAwait(false);
            return await handle.ExtractAsync(entry.Index, layer, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    ///     Stops indexing, releases the index and disposes the synchronization gate; the loaded package
    ///     itself is left alone.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loadCancellation?.Cancel();
        _entries = [];
        IsLoaded = false;
        _loadCancellation?.Dispose();
        _gate.Dispose();
    }

    /// <summary>
    ///     Indexes the tile sets in the background and publishes the result.
    /// </summary>
    /// <param name="progress">Reports how many tile sets have been indexed.</param>
    /// <param name="cancellationToken">Cancels the indexing.</param>
    private async Task IndexAsync(IProgress<VirtualTextureIndexProgress>? progress,
        CancellationToken cancellationToken)
    {
        var entries = await Task.Run(() => IndexEntries(progress, cancellationToken), CancellationToken.None)
            .ConfigureAwait(false);
        _entries = entries;
        IsLoaded = true;
    }

    /// <summary>
    ///     Reads the metadata of every tile set of the package.
    /// </summary>
    /// <param name="progress">Reports how many tile sets have been indexed.</param>
    /// <param name="cancellationToken">Cancels the indexing.</param>
    private List<VirtualTextureEntry> IndexEntries(IProgress<VirtualTextureIndexProgress>? progress,
        CancellationToken cancellationToken)
    {
        var tileSets = packageService.GetValidFiles()
            .Where(file => FileExtensions.IsVirtualTexture(Path.GetExtension(file.Name)))
            .ToList();
        var results = new List<VirtualTextureEntry>?[tileSets.Count];
        var indexed = 0;
        var stopwatch = Stopwatch.StartNew();

        // The tile sets are independent, so their metadata is read in parallel; the entries keep the
        // package order because each result is stored at the index of the tile set it came from.
        Parallel.For(0, tileSets.Count, new ParallelOptions { CancellationToken = cancellationToken },
            index =>
            {
                var path = tileSets[index].Name;
                try
                {
                    results[index] = ReadTileSet(path);
                }
                catch (Exception ex)
                {
                    // A single unreadable tile set must not cost the textures of all the others.
                    Log.Warning(ex, "Failed to index virtual texture tile set: {Path}", path);
                    results[index] = [];
                }

                progress?.Report(new VirtualTextureIndexProgress(Interlocked.Increment(ref indexed),
                    tileSets.Count));
            });

        var entries = results.SelectMany(result => result ?? []).ToList();
        Log.Information("Indexed {TextureCount} virtual textures from {TileSetCount} tile set(s) in {Elapsed} ms",
            entries.Count, tileSets.Count, stopwatch.ElapsedMilliseconds);
        return entries;
    }

    /// <summary>
    ///     Reads the textures of one tile set, without opening any of its page files.
    /// </summary>
    /// <param name="gtsPath">The package path of the tile set.</param>
    /// <returns>The textures of the tile set, in index order.</returns>
    private List<VirtualTextureEntry> ReadTileSet(string gtsPath)
    {
        using var stream = packageService.GetFileByPath(gtsPath)?.CreateContentReader()
                           ?? throw new FileNotFoundException($"Virtual texture not found in package: {gtsPath}");
        using var loader = new VirtualTextureLoader(stream);
        var textures = loader.GetTextures();
        var entries = new List<VirtualTextureEntry>(textures.Count);
        entries.AddRange(textures.Select((meta, index) =>
            new VirtualTextureEntry(gtsPath, Path.GetFileName(meta.Name), loader.LayerCount, index)));

        return entries;
    }

    /// <summary>
    ///     Opens one tile set of the package.
    /// </summary>
    /// <param name="gtsPath">The package path of the tile set.</param>
    /// <returns>The handle, which the caller owns and disposes.</returns>
    private VirtualTextureTileSetHandle CreateHandle(string gtsPath)
    {
        var directory = Path.GetDirectoryName(gtsPath)?.Replace('\\', '/') ?? string.Empty;
        var stream = packageService.GetFileByPath(gtsPath)?.CreateContentReader()
                     ?? throw new FileNotFoundException($"Virtual texture not found in package: {gtsPath}");
        VirtualTextureLoader? loader = null;
        try
        {
            // The page files are opened by the extraction that reads them, so nothing is opened here.
            loader = new VirtualTextureLoader(stream);
            return new VirtualTextureTileSetHandle(gtsPath, stream, loader,
                pageName => VirtualTexturePageFile.Open(packageService, directory, pageName), loader.GetTextures());
        }
        catch
        {
            loader?.Dispose();
            stream.Dispose();
            throw;
        }
    }
}