using System.Diagnostics;
using System.Runtime.CompilerServices;
using BG3PakViewer.Contracts;
using BG3PakViewer.Utils;
using BG3PakViewer.VirtualTextures.Models;
using BG3PakViewer.VirtualTextures.TileSets;
using Serilog;

namespace BG3PakViewer.VirtualTextures.Catalog;

/// <summary>
///     Streams every virtual texture of the loaded package as its tile sets (.gts) are read.
/// </summary>
/// <remarks>
///     The enumeration is pull-driven: a tile set is parsed only when the caller asks for its textures,
///     and its list is released before the next one starts. Nothing is cached between enumerations, so
///     the peak memory is one tile set rather than the whole index.
/// </remarks>
/// <param name="packageService">The loaded package the tile sets are read from.</param>
public sealed class VirtualTextureCatalog(IPackageService packageService) : IVirtualTextureCatalog
{
    private bool _disposed;

    /// <inheritdoc />
    public async IAsyncEnumerable<VirtualTextureEntry> LoadAsync(
        IProgress<VirtualTextureIndexProgress>? progress,
        ICollection<VirtualTextureTileSetFailure> failures,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var stopwatch = Stopwatch.StartNew();

        var files = await packageService.Search().ConfigureAwait(false);
        if (files is null) yield break;

        // The progress total needs a count, but walking the package files twice is cheaper than
        // materializing another list of path strings.
        var total = files.Count(file => FileExtensions.IsVirtualTexture(Path.GetExtension(file.Name)));
        progress?.Report(new VirtualTextureIndexProgress(0, total));

        var indexed = 0;
        var textureCount = 0;
        var failureCount = 0;
        foreach (var file in files)
        {
            if (!FileExtensions.IsVirtualTexture(Path.GetExtension(file.Name))) continue;
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<VirtualTextureEntry>? entries = null;
            try
            {
                // LSLib parses a tile set synchronously, so it runs on a worker. Only this one tile set
                // is materialized, and it is released as soon as its entries have been yielded.
                entries = await Task.Run(() => ReadTileSetAsync(file.Name, cancellationToken), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One unreadable tile set must not cost the textures of all the others.
                Log.Warning(ex, "Failed to index virtual texture tile set: {Path}", file.Name);
                failureCount++;
                failures.Add(new VirtualTextureTileSetFailure(file.Name, ex.Message));
            }

            if (entries is not null)
                foreach (var entry in entries)
                {
                    textureCount++;
                    yield return entry;
                }

            progress?.Report(new VirtualTextureIndexProgress(++indexed, total));
        }

        Log.Information("Indexed {TextureCount} virtual textures from {TileSetCount} tile set(s) in {Elapsed} ms",
            textureCount, total, stopwatch.ElapsedMilliseconds);
        // Kept next to the totals so that a tile set which stopped parsing shows up in the log even
        // when nobody opens the toolbox and sees the warning there.
        if (failureCount != 0)
            Log.Warning("Failed to index {FailureCount} of {TileSetCount} virtual texture tile set(s)",
                failureCount, total);
    }

    /// <summary>
    ///     Marks the catalog as disposed. Nothing is kept between enumerations, so a running
    ///     enumeration is stopped by cancelling its token instead.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Reads one tile set on a worker; the caller yields its textures and then releases the list.
    /// </summary>
    private async Task<IReadOnlyList<VirtualTextureEntry>> ReadTileSetAsync(
        string gtsPath, CancellationToken cancellationToken)
    {
        var fileInfo = await packageService.GetFileByPath(gtsPath).ConfigureAwait(false);
        var stream = await Task.Run(() => fileInfo?.CreateContentReader(), cancellationToken).ConfigureAwait(false);
        if (stream is null) throw new FileNotFoundException($"Virtual texture not found in package: {gtsPath}");
        var tileSetFile = new VirtualTextureTileSetFile(stream);
        return
        [
            .. tileSetFile.Textures.Select((meta, index) =>
                new VirtualTextureEntry(gtsPath, Path.GetFileName(meta.Name), tileSetFile.LayerCount, index))
        ];
    }
}
