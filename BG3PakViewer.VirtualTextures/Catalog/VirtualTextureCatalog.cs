using System.Diagnostics;
using BG3PakViewer.Contracts;
using BG3PakViewer.Utils;
using BG3PakViewer.VirtualTextures.Extraction;
using BG3PakViewer.VirtualTextures.Models;
using BG3PakViewer.VirtualTextures.TileSets;
using Serilog;

namespace BG3PakViewer.VirtualTextures.Catalog;

/// <summary>
///     Indexes every virtual texture tile set (.gts) of the loaded package.
/// </summary>
/// <remarks>
///     The index holds metadata only, so it opens the tile sets, reads their texture names and closes
///     them again: it never opens a GTP page file. The layers themselves are produced by
///     <see cref="IVirtualTextureExtractor" />. <see cref="ClearAsync" /> waits for the work in progress
///     before releasing the index, which is what lets the package be closed safely.
/// </remarks>
/// <param name="packageService">The loaded package the tile sets are read from.</param>
public sealed class VirtualTextureCatalog(IPackageService packageService) : IVirtualTextureCatalog
{
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
    ///     Stops indexing and releases the index, so the package can be closed afterward.
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
        _entries = [];
        IsLoaded = false;
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        _loadTask = null;
    }

    /// <summary>
    ///     Stops indexing and releases the index; the loaded package itself is left alone.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loadCancellation?.Cancel();
        _entries = [];
        IsLoaded = false;
        _loadCancellation?.Dispose();
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
        using var file = new VirtualTileSetFile(
            packageService.GetFileByPath(gtsPath)?.CreateContentReader()
            ?? throw new FileNotFoundException($"Virtual texture not found in package: {gtsPath}"));

        return file.Textures.Select((meta, index) =>
            new VirtualTextureEntry(gtsPath, Path.GetFileName(meta.Name), file.LayerCount, index)).ToList();
    }
}