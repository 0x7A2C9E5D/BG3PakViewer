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
///     be parsed contributes a <see cref="VirtualTextureIndexFailure" /> instead of aborting the index,
///     and progress is reported once per tile set. Cancelling the token throws rather than returning a
///     partial index. Nothing is cached between calls, so every call reads the tile sets again and the
///     package has to stay loaded until it finishes; the catalog holds no resources of its own and
///     needs no disposal.
/// </remarks>
/// <param name="packageService">The loaded package the tile sets are read from.</param>
public sealed class VirtualTextureCatalog(IPackageService packageService) : IVirtualTextureCatalog
{
    /// <summary>
    ///     Indexes every virtual texture tile set of the loaded package.
    /// </summary>
    /// <remarks>
    ///     A package with no active files produces an empty index instead of throwing. Cancellation
    ///     throws <see cref="OperationCanceledException" /> rather than returning a partial index, and
    ///     a tile set that cannot be parsed is reported in the result instead of failing the call. The
    ///     package has to stay loaded until the returned task completes.
    /// </remarks>
    /// <param name="progress">Receives the number of tile sets indexed so far; may be <see langword="null" />.</param>
    /// <param name="cancellation">Stops indexing.</param>
    /// <returns>Every texture of the package, plus the tile sets that could not be read.</returns>
    public async Task<VirtualTextureIndexResult> IndexAsync(
        IProgress<VirtualTextureIndexProgress>? progress,
        CancellationToken cancellation = default)
    {
        var files = await packageService.Search().ConfigureAwait(false);
        if (files is null) return new VirtualTextureIndexResult([], []);

        return await IndexFilesAsync(files, progress, cancellation).ConfigureAwait(false);
    }

    /// <summary>
    ///     Builds the index from every tile set in <paramref name="files" />, reporting progress as
    ///     tile sets complete and logging the totals when the index is finished.
    /// </summary>
    /// <remarks>
    ///     A tile set that fails to parse is recorded and skipped, so one unreadable file does not
    ///     hide the textures of all the others. Progress counts handled tile sets, including the ones
    ///     that failed.
    /// </remarks>
    /// <param name="files">The package's active files, including entries that are not tile sets.</param>
    /// <param name="progress">Receives the number of tile sets handled so far; may be <see langword="null" />.</param>
    /// <param name="cancellation">Stops at the next tile set by throwing <see cref="OperationCanceledException" />.</param>
    /// <returns>Every texture, plus the tile sets that could not be read.</returns>
    private async Task<VirtualTextureIndexResult> IndexFilesAsync(
        IReadOnlyList<PackagedFileInfo> files,
        IProgress<VirtualTextureIndexProgress>? progress,
        CancellationToken cancellation)
    {
        var indexed = 0;
        var total = CountIndexable(files);
        progress?.Report(new VirtualTextureIndexProgress(0, total));
        var entries = new List<VirtualTextureEntry>();
        var failures = new List<VirtualTextureIndexFailure>();
        foreach (var file in files)
        {
            if (!IsIndexable(file)) continue;
            cancellation.ThrowIfCancellationRequested();

            var result = await IndexFileAsync(file, cancellation).ConfigureAwait(false);
            entries.AddRange(result.entries);
            failures.AddRange(result.failures);

            progress?.Report(new VirtualTextureIndexProgress(++indexed, total));
        }

        LogSummary(entries.Count, total, failures.Count);
        return new VirtualTextureIndexResult(entries, failures);
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
    ///     Indexes one tile set on a worker, returning either its textures or the reason it could not
    ///     be read. A canceled read is rethrown so indexing stops.
    /// </summary>
    /// <param name="file">The tile set's entry in the package.</param>
    /// <param name="cancellation">Cancels the read.</param>
    /// <returns>The tile set's textures, or a single failure when it could not be parsed.</returns>
    private static async Task<VirtualTextureIndexResult> IndexFileAsync(PackagedFileInfo file,
        CancellationToken cancellation)
    {
        try
        {
            // Only this one tile set is materialized, and it is released once its entries have been
            // copied into the index.
            var entries = await ParseEntriesAsync(file, cancellation).ConfigureAwait(false);
            return new VirtualTextureIndexResult(entries, []);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One unreadable tile set must not cost the textures of all the others.
            Log.Warning(ex, "Failed to index virtual texture tile set: {Path}", file.Name);
            return new VirtualTextureIndexResult([], [new VirtualTextureIndexFailure(file.Name, ex.Message)]);
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
    ///     into the index and then releases the list.
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
}
