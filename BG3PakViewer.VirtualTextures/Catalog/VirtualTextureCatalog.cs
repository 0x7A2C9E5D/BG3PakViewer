using BG3PakViewer.Contracts;
using BG3PakViewer.Utils;
using BG3PakViewer.VirtualTextures.Models;
using BG3PakViewer.VirtualTextures.TileSets;
using LSLib.LS;
using Serilog;

namespace BG3PakViewer.VirtualTextures.Catalog;

/// <summary>
///     Indexes every virtual texture of the loaded package from its tile sets (.gts).
/// </summary>
/// <remarks>
///     Tile sets are parsed one at a time on a worker, so only one is materialized while the index is
///     built; the complete list of textures is returned when the read ends. Nothing is cached between
///     calls, so every call reads the tile sets again and the package has to stay loaded until it ends.
/// </remarks>
/// <param name="packageService">The loaded package the tile sets are read from.</param>
public sealed class VirtualTextureCatalog(IPackageService packageService) : IVirtualTextureCatalog
{
    /// <inheritdoc />
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
    /// <param name="files">The package's active files.</param>
    /// <param name="progress">Receives the number of tile sets indexed so far.</param>
    /// <param name="cancellation">Cancels indexing.</param>
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
            entries.AddRange(result.Entries);
            failures.AddRange(result.Failures);

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
    ///     Tells whether a package file is a virtual texture tile set.
    /// </summary>
    private static bool IsIndexable(PackagedFileInfo file)
    {
        return FileExtensions.IsVirtualTexture(Path.GetExtension(file.Name));
    }

    /// <summary>
    ///     Indexes one tile set on a worker, returning either its textures or the reason it could not
    ///     be read. A canceled read is rethrown so indexing stops.
    /// </summary>
    /// <param name="cancellation">Cancels the read.</param>
    /// <returns>The tile set's textures, or a single failure when it could not be parsed.</returns>
    private async Task<VirtualTextureIndexResult> IndexFileAsync(PackagedFileInfo file,
        CancellationToken cancellation)
    {
        try
        {
            // LSLib parses a tile set synchronously, so it runs on a worker. Only this one tile set
            // is materialized, and it is released once its entries have been copied to the index.
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
    /// <param name="tileSetCount">How many tile sets the package holds.</param>
    /// <param name="failureCount">How many tile sets failed to parse.</param>
    private static void LogSummary(int textureCount, int tileSetCount, int failureCount)
    {
        Log.Information("Indexed {TextureCount} virtual textures from {TileSetCount} tile set(s)",
            textureCount, tileSetCount);
        // Kept next to the totals so that a tile set which stopped parsing shows up in the log even
        // when nobody opens the toolbox and sees the warning there.
        if (failureCount != 0)
            Log.Warning("Failed to index {FailureCount} of {TileSetCount} virtual texture tile set(s)",
                failureCount, tileSetCount);
    }

    /// <summary>
    ///     Parses one tile set's metadata on a worker; the caller copies the entries into the index
    ///     and then releases the list.
    /// </summary>
    private static async Task<IReadOnlyList<VirtualTextureEntry>> ParseEntriesAsync(
        PackagedFileInfo file, CancellationToken cancellationToken)
    {
        var stream = await Task.Run(file.CreateContentReader, cancellationToken).ConfigureAwait(false);
        if (stream is null) throw new FileNotFoundException($"Virtual texture not found in package: {file.Name}");
        var tileSetFile = new VirtualTextureTileSetFile(stream);
        return
        [
            .. tileSetFile.Textures.Select((meta, index) =>
                new VirtualTextureEntry(file.Name, Path.GetFileName(meta.Name), tileSetFile.LayerCount, index))
        ];
    }
}
