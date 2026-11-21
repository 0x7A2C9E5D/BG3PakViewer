using BG3PakViewer.Contracts;
using BG3PakViewer.Utils;
using BG3PakViewer.VirtualTextures.Models;
using BG3PakViewer.VirtualTextures.TileSets;
using LSLib.LS;
using Serilog;

namespace BG3PakViewer.VirtualTextures.Catalog;

/// <summary>
///     Indexes the virtual textures of the loaded package from its tile set (.gts) metadata.
/// </summary>
/// <remarks>
///     Only tile set metadata is read; GTP page files are never opened, so indexing stays cheap
///     compared with extracting a texture layer. Tile sets are parsed one at a time on a worker, so
///     at most one is materialized while the index is built, and the complete
///     <see cref="VirtualTextureIndexResult" /> is returned once the read ends. A tile set that cannot
///     be parsed is retried once before it contributes a <see cref="VirtualTextureIndexFailure" />
///     instead of aborting the index, and progress is reported once per tile set. Cancelling the token
///     throws rather than returning a partial index. At the end of a build the mutable lists are
///     wrapped in read-only views. Only a complete index is cached with the
///     <see cref="IPackageService.PackageVersion" /> it was built from and shared by every caller
///     until the loaded package changes: a cached failure would keep its missing textures and its
///     warning alive for every later caller, so an incomplete result is returned without being cached
///     and the next caller rebuilds it. An index built while the package is replaced is discarded
///     instead of cached, because the old package's streams are already released. The catalog owns only
///     its gate: it is released by <see cref="Dispose" /> when the application shuts down, while the
///     loaded package belongs to the composition root.
/// </remarks>
/// <param name="packageService">The loaded package the tile sets are read from.</param>
public sealed class VirtualTextureCatalog(IPackageService packageService) : IVirtualTextureCatalog, IDisposable
{
    /// <summary>
    ///     Serializes indexing and cache reads. Closing the toolbox cancels its index without waiting
    ///     for it, so reopening the toolbox can start a second run while the first is still reading a
    ///     tile set; the package is not safe for concurrent reads. The gate also publishes the cache
    ///     written on a worker to the next caller, so no volatile reads or double checks are needed.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    ///     The index of the package version recorded in <see cref="CachedIndex" />, or
    ///     <see langword="null" /> when no valid index has been built yet. Read and written only
    ///     while holding <see cref="_gate" />.
    /// </summary>
    private CachedIndex? _cache;

    private bool _disposed;

    /// <summary>
    ///     Releases the indexing gate when it is idle. An index still running keeps it and the gate is
    ///     left to the garbage collector: it never allocates a wait handle, so nothing leaks and this
    ///     never blocks the thread that is shutting the application down.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        // Acquire without waiting: a gate that is still held must not be disposed under the index that
        // will release it. Taking it atomically also closes the check-then-dispose race, and the gate is
        // disposed while it is already ours, so no release is needed.
        if (!_gate.Wait(0)) return;
        _gate.Dispose();
        _disposed = true;
    }

    /// <summary>
    ///     Returns the package's virtual texture index, building it only when it is missing or stale.
    /// </summary>
    /// <remarks>
    ///     A package with no active files produces an empty index instead of throwing; that empty
    ///     index is not cached. Cancellation throws <see cref="OperationCanceledException" /> rather
    ///     than returning a partial index, both while building and on a cache hit, so closing the
    ///     toolbox behaves the same either way. A tile set that cannot be parsed is reported in the
    ///     result instead of failing the call. When the package changes while the index is built, the
    ///     run is abandoned rather than cached or returned. A cache hit returns the shared read-only
    ///     result without touching the package.
    /// </remarks>
    /// <param name="progress">
    ///     Receives the number of tile sets indexed so far; may be <see langword="null" />. A cache hit
    ///     reports the final tile set count once.
    /// </param>
    /// <param name="cancellation">Stops indexing, and also cancels an otherwise immediate cache hit.</param>
    /// <returns>
    ///     Every texture in package and tile set order, plus the tile sets that could not be read, as
    ///     read-only collections shared with every other caller.
    /// </returns>
    public async Task<VirtualTextureIndexResult> IndexAsync(
        IProgress<VirtualTextureIndexProgress>? progress,
        CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        await _gate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            var version = packageService.PackageVersion;
            if (_cache is { } cached && cached.Version == version)
            {
                progress?.Report(new VirtualTextureIndexProgress(cached.TileSetTotal, cached.TileSetTotal));
                return cached.Result;
            }

            // A stale cache belongs to a package that may already be released; drop the reference as
            // soon as it stops being usable instead of holding two indexes alive until the next one
            // is finished.
            _cache = null;

            var files = await packageService.Search().ConfigureAwait(false);
            if (files is null) return new VirtualTextureIndexResult([], []);

            var total = CountIndexable(files);
            var result = await IndexFilesAsync(files, total, version, progress, cancellation)
                .ConfigureAwait(false);

            // The old package is disposed as soon as another one is loaded. Its streams would have
            // produced a partial index, which must not be published or cached under the new version.
            if (packageService.PackageVersion != version)
                throw new OperationCanceledException("The package changed while the index was built.");

            // IndexFilesAsync already returned read-only views, so the cache can hand this very
            // result to every later caller: no extra copy or freeze is needed. A result with
            // failures is deliberately not cached: a cache hit never touches the package again, so
            // caching it would keep the missing textures and the warning for the rest of the session
            // instead of letting the next caller rebuild the index and recover them.
            if (result.failures.Count == 0)
                _cache = new CachedIndex(version, result, total);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    ///     Builds the index from every tile set in <paramref name="files" />, reporting progress as
    ///     tile sets complete and logging the totals when the index is finished.
    /// </summary>
    /// <remarks>
    ///     A tile set that fails to parse is recorded and skipped, so one unreadable file does not
    ///     hide the textures of all the others. Progress counts handled tile sets, including the ones
    ///     that failed. A package replaced during the read ends the run: its streams are released, so
    ///     carrying on would turn disposed reads into bogus failure entries. Entries and failures stay
    ///     in package and tile set order; the two mutable lists used while building are wrapped in
    ///     read-only views only once, when the result is returned.
    /// </remarks>
    /// <param name="files">The package's active files, including entries that are not tile sets.</param>
    /// <param name="total">The number of tile sets in <paramref name="files" />.</param>
    /// <param name="version">The package version the files came from.</param>
    /// <param name="progress">Receives the number of tile sets handled so far; may be <see langword="null" />.</param>
    /// <param name="cancellation">Stops at the next tile set by throwing <see cref="OperationCanceledException" />.</param>
    /// <returns>
    ///     Every texture and every failed tile set, in package and tile set order, as read-only views.
    /// </returns>
    private async Task<VirtualTextureIndexResult> IndexFilesAsync(
        IReadOnlyList<PackagedFileInfo> files,
        int total,
        long version,
        IProgress<VirtualTextureIndexProgress>? progress,
        CancellationToken cancellation)
    {
        var indexed = 0;
        progress?.Report(new VirtualTextureIndexProgress(0, total));
        var entries = new List<VirtualTextureEntry>();
        var failures = new List<VirtualTextureIndexFailure>();
        foreach (var file in files)
        {
            if (!IsIndexable(file)) continue;
            cancellation.ThrowIfCancellationRequested();
            if (packageService.PackageVersion != version)
                throw new OperationCanceledException("The package changed while the index was built.");

            var result = await IndexFileAsync(file, cancellation).ConfigureAwait(false);
            entries.AddRange(result.entries);
            failures.AddRange(result.failures);

            progress?.Report(new VirtualTextureIndexProgress(++indexed, total));
        }

        LogSummary(entries.Count, total, failures.Count);
        return new VirtualTextureIndexResult(entries.AsReadOnly(), failures.AsReadOnly());
    }

    /// <summary>
    ///     Counts the tile sets without materializing a second list of paths: walking the package files
    ///     twice is cheaper than keeping another list of path strings alive.
    /// </summary>
    /// <param name="files">The package's active files.</param>
    /// <returns>How many of them are virtual texture tile sets.</returns>
    private static int CountIndexable(IReadOnlyList<PackagedFileInfo> files)
    {
        return files.Count(IsIndexable);
    }

    /// <summary>
    ///     Tells whether a package file is a virtual texture tile set, by its extension.
    /// </summary>
    /// <param name="file">The package file to check.</param>
    /// <returns><see langword="true" /> when the file is a virtual texture tile set.</returns>
    private static bool IsIndexable(PackagedFileInfo file)
    {
        return FileExtensions.IsVirtualTexture(Path.GetExtension(file.Name));
    }

    /// <summary>
    ///     Indexes one tile set on a worker, retrying a transient read failure once before giving up,
    ///     and returning either its textures or the reason it could not be read. A canceled read is
    ///     rethrown so indexing stops.
    /// </summary>
    /// <remarks>
    ///     LSLib's struct reader occasionally fails on an otherwise valid tile set with a generic-type
    ///     or GCHandle error, while reading the same file again succeeds. Retrying here keeps that
    ///     hiccup out of the index and out of the warning the toolbox shows. Every attempt opens a
    ///     fresh stream, so a retry never reuses the state that failed.
    /// </remarks>
    /// <param name="file">The tile set's entry in the package.</param>
    /// <param name="cancellation">Cancels the read and the retry delay.</param>
    /// <returns>The tile set's textures, or a single failure when it could not be parsed.</returns>
    private static async Task<VirtualTextureIndexResult> IndexFileAsync(PackagedFileInfo file,
        CancellationToken cancellation)
    {
        const int maxAttempts = 2;
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                // Only this one tile set is materialized, and it is released once its entries have
                // been copied into the index.
                var entries = await ParseEntriesAsync(file, cancellation).ConfigureAwait(false);
                if (attempt > 1)
                    Log.Information("Indexed virtual texture tile set {Path} after {Attempt} attempts",
                        file.Name, attempt);
                return new VirtualTextureIndexResult(entries, []);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= maxAttempts)
                {
                    // One unreadable tile set must not cost the textures of all the others.
                    Log.Warning(ex, "Failed to index virtual texture tile set: {Path}", file.Name);
                    return new VirtualTextureIndexResult([],
                        [new VirtualTextureIndexFailure(file.Name, ex.Message)]);
                }

                Log.Warning(ex,
                    "Transient failure indexing virtual texture tile set {Path}; retrying ({Attempt}/{MaxAttempts})",
                    file.Name, attempt, maxAttempts);
                // Let an overlapping read that tripped LSLib's shared state finish before retrying.
                await Task.Delay(50, cancellation).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Logs the index totals, including the tile sets that could not be read.
    /// </summary>
    /// <param name="textureCount">How many textures were indexed.</param>
    /// <param name="tileSetCount">How many tile sets the package holds, including failed ones.</param>
    /// <param name="failureCount">How many tile sets failed to parse.</param>
    private static void LogSummary(int textureCount, int tileSetCount, int failureCount)
    {
        Log.Information("indexed {TextureCount} virtual textures from {TileSetCount} tile set(s)",
            textureCount, tileSetCount);
        // Kept next to the totals so that a tile set which stopped parsing shows up in the log even
        // when nobody opens the toolbox and sees the warning there.
        if (failureCount != 0)
            Log.Warning("Failed to index {FailureCount} of {TileSetCount} virtual texture tile set(s)",
                failureCount, tileSetCount);
    }

    /// <summary>
    ///     Parses one tile set's metadata into texture entries on a worker; the caller copies them
    ///     into the index, so only this one tile set is materialized at a time.
    /// </summary>
    /// <param name="file">The tile set's entry in the package.</param>
    /// <param name="cancellationToken">Cancels the synchronous read.</param>
    /// <returns>The textures of this tile set, in tile set order.</returns>
    private static async Task<IReadOnlyList<VirtualTextureEntry>> ParseEntriesAsync(
        PackagedFileInfo file, CancellationToken cancellationToken)
    {
        // LSLib reads the tile set synchronously, so it runs on a worker; ConfigureAwait(false) keeps
        // the metadata parse on that worker too instead of returning to the UI thread.
        var stream = await Task.Run(file.CreateContentReader, cancellationToken).ConfigureAwait(false);
        if (stream is null) throw new FileNotFoundException($"Virtual texture not found in package: {file.Name}");
        await using var tileSetFile = new VirtualTextureTileSetSlot(stream);
        var layerCount = tileSetFile.LayerCount;
        var textures = tileSetFile.Textures;
        return
        [
            .. textures.Select((t, i) =>
                new VirtualTextureEntry(file.Name, Path.GetFileName(t.Name), layerCount, i))
        ];
    }

    /// <summary>
    ///     An index, the package version it was built from, and the tile set count used for progress
    ///     reporting on a cache hit.
    /// </summary>
    private sealed record CachedIndex(long Version, VirtualTextureIndexResult Result, int TileSetTotal);
}
