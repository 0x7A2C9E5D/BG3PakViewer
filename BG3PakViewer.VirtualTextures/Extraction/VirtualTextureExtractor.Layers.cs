using System.Drawing;
using BG3PakViewer.VirtualTextures.Models;
using BG3PakViewer.VirtualTextures.PageFiles;
using BG3PakViewer.VirtualTextures.TileSets;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

// The layer lookup and DDS writing half of VirtualTextureExtractor. It runs inside the gate and reads
// the cached tile set directly, so no tile set, texture metadata or page file path has to be threaded
// through the call chain. One Task.Run covers finding the mip level and writing all of its tile bands.
public sealed partial class VirtualTextureExtractor
{
    private readonly TileCompressor _compressor = new();

    /// <summary>
    ///     Writes the first mip level that holds <paramref name="layer" /> into a seekable DDS stream.
    /// </summary>
    /// <param name="entry">The texture to extract.</param>
    /// <param name="layer">The layer index.</param>
    /// <param name="progress">Reports how many tile rows have been written.</param>
    /// <param name="ct">Cancels the extraction.</param>
    /// <returns>
    ///     A seekable DDS stream positioned at its start, or <see langword="null" /> when the layer is
    ///     empty at every mip level. <c>IVirtualTextureExtractor.ExtractLayerAsync</c> reports an empty
    ///     layer as null, so the nullability warning is suppressed on that branch.
    /// </returns>
    private Task<Stream> TranscodeLayer(VirtualTextureEntry entry, int layer,
        IProgress<(int Done, int Total)>? progress, CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            var tex = _tileSet!.Textures[entry.index];
            // An empty layer is a normal result, not an error: the interface reports it as null.
            if (FindLevel(layer, tex, ct) is not { } found) return null!;

            var tileSet = _tileSet.TileSet;
            var (mip, min, max) = found;
            var size = VirtualTextureTileRangeCalculator.GetTileRangeSize(min, max);
            var stream = new MemoryStream();
            var tile = tileSet.TileSize;
            var decoder = new VirtualTextureChunkDecoder(tileSet.ParameterBlocks, tile, _compressor);
            await using var unpacker = new VirtualTextureUnpacker(tileSet.PageSize,
                async pageFileIndex => await OpenPageFileAsync(tileSet.PageFileInfos[pageFileIndex].FileName),
                decoder);
            var writer = new VirtualTextureWriter(size, tile, tileSet.TileBorder, unpacker, stream);
            await writer.WriteHeader();
            // The DDS has a fixed size, so the buffer is grown once instead of doubling while the bands are
            // written.
            stream.Capacity = (int)(stream.Length + writer.RowByteCount * size.Height);
            var rowTiles = new GTSFlatTileInfo?[size.Width];
            for (var row = 0; row < size.Height; row++)
            {
                ct.ThrowIfCancellationRequested();
                FillRows(mip, layer, new Point(min.X, min.Y + row), rowTiles);
                await writer.WriteRow(rowTiles);
                progress?.Report((row + 1, size.Height));
            }

            stream.Position = 0;
            return (Stream)stream;
        }, ct);
    }

    /// <summary>
    ///     Finds the first mip level whose tile region exists in a layer.
    /// </summary>
    /// <param name="layer">The layer index.</param>
    /// <param name="tex">The texture to look up.</param>
    /// <param name="ct">Cancels the search.</param>
    /// <returns>The level and its region, or <see langword="null" /> when no level holds the layer.</returns>
    private (int Level, Point Min, Point Max)? FindLevel(int layer, FourCCTextureMeta tex,
        CancellationToken ct = default)
    {
        var tileSet = _tileSet!.TileSet;
        for (var level = 0; level < tileSet.TileSetLevels.Length; level++)
        {
            ct.ThrowIfCancellationRequested();
            if (VirtualTextureTileRangeCalculator.GetTileRange(tileSet, level, tex) is not { } range) continue;
            var (min, max) = range;
            if (!VirtualTextureTileRangeCalculator.RegionExists(tileSet, level, layer, min, max)) continue;

            return (level, min, max);
        }

        return null;
    }

    /// <summary>
    ///     Looks up the tiles of one band in the tile set, leaving a column empty when the tile set has no
    ///     tile there.
    /// </summary>
    /// <param name="level">The index of the mip level the tiles are read from.</param>
    /// <param name="layer">The layer index the tiles are read from.</param>
    /// <param name="origin">The tile column and row the band starts at.</param>
    /// <param name="rows">The buffer the band is resolved into, one entry per column.</param>
    private void FillRows(int level, int layer, Point origin, Span<GTSFlatTileInfo?> rows)
    {
        var tileSet = _tileSet!.TileSet;
        for (var col = 0; col < rows.Length; col++)
        {
            GTSFlatTileInfo tile = default;
            rows[col] = tileSet.GetTileInfo(level, layer, origin.X + col, origin.Y, ref tile) ? tile : null;
        }
    }

    /// <summary>
    ///     Creates a progress reporter for tile extraction.
    /// </summary>
    /// <param name="progress">The progress reported to the caller, in percent.</param>
    /// <returns>
    ///     A reporter that turns the written tile rows into percent, or <see langword="null" /> when the
    ///     caller does not want progress.
    /// </returns>
    private static Progress<(int Done, int Total)>? CreateProgress(IProgress<double>? progress)
    {
        if (progress is null) return null;
        return new Progress<(int Done, int Total)>(p =>
            progress.Report(p.Total == 0 ? 0 : p.Done * 100.0 / p.Total));
    }
}
