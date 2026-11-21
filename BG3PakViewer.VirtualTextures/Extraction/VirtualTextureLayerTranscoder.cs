using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using BG3PakViewer.VirtualTextures.PageFiles;
using BG3PakViewer.VirtualTextures.TileSets;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Turns one layer of a parsed tile set into a single-mip DDS stream.
/// </summary>
/// <remarks>
///     The transcoder holds no package or cache state: the tile set, its tile compressor and the way its
///     page files are opened are handed in, so it runs without the extractor. Page files are opened
///     lazily through the provider, and the row producer/consumer split lets a shared uniform tile be
///     copied before the next one is decoded.
/// </remarks>
/// <param name="tileSet">The parsed tile set the layer belongs to.</param>
/// <param name="compressor">The decompressor the compressed chunks are unpacked with.</param>
/// <param name="pageFileProvider">Opens the page file at the given index.</param>
internal sealed class VirtualTextureLayerTranscoder(
    VirtualTileSet tileSet,
    TileCompressor compressor,
    Func<int, Task<Stream>> pageFileProvider)
{
    /// <summary>
    ///     Writes the first mip level that holds <paramref name="texture" /> into a seekable DDS stream.
    /// </summary>
    /// <param name="texture">The texture metadata, which places the texture on the level 0 tile grid.</param>
    /// <param name="layer">The layer index.</param>
    /// <param name="progress">Reports the extraction progress, in percent.</param>
    /// <param name="ct">Cancels the extraction.</param>
    /// <returns>
    ///     A seekable DDS stream positioned at its start, or <see langword="null" /> when the layer is
    ///     empty at every mip level.
    /// </returns>
    public async Task<Stream?> TranscodeAsync(FourCCTextureMeta texture, int layer,
        IProgress<double>? progress, CancellationToken ct)
    {
        // An empty layer is a normal result, not an error: the interface reports it as null.
        if (VirtualTextureTileRangeCalculator.FindLevel(tileSet, layer, texture, ct) is not { } found) return null;

        var (mip, min, max) = found;
        var size = VirtualTextureTileRangeCalculator.GetTileRangeSize(min, max);
        var stream = new MemoryStream();
        var tile = tileSet.TileSize;
        var decoder = new VirtualTextureChunkDecoder(tileSet.ParameterBlocks, compressor, tile);
        await using var unpacker = new VirtualTextureUnpacker(decoder, pageFileProvider, tileSet.PageSize);
        await using var writer = new VirtualTextureWriter(size, tile, tileSet.TileBorder, stream);
        await writer.WriteHeader().ConfigureAwait(false);
        // The DDS has a fixed size, so the buffer is grown once instead of doubling while the bands are
        // written.
        stream.Capacity = (int)(stream.Length + writer.RowByteCount * size.Height);
        var rowTiles = new GTSFlatTileInfo?[size.Width];
        // One reader for every band: the callback reads the band buffer, which FillRows rewrites before
        // each call, so the tiles stay lazy and a shared uniform image is copied before the next one.
        var readTile = CreateRowReader(unpacker, rowTiles, ct);
        var rowProgress = CreateProgress(progress);
        for (var row = 0; row < size.Height; row++)
        {
            ct.ThrowIfCancellationRequested();
            FillRows(mip, layer, min with { Y = min.Y + row }, rowTiles);
            await writer.WriteRow(rowTiles.Length, readTile).ConfigureAwait(false);
            rowProgress?.Report((row + 1, size.Height));
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    ///     Creates the lazy reader the writer pulls one band's tiles through.
    /// </summary>
    /// <remarks>
    ///     The unpacker is a parameter here instead of the caller's <c>await using</c> local, so the
    ///     returned reader does not capture a disposable declared in the caller's scope.
    /// </remarks>
    /// <param name="unpacker">The unpacker the tiles are read through.</param>
    /// <param name="rowTiles">The band buffer, rewritten by <see cref="FillRows" /> before every band.</param>
    /// <param name="ct">Cancels the decompression.</param>
    /// <returns>A reader that decompresses the tile at a column, or returns null for an empty column.</returns>
    private static Func<int, Task<BC5Image?>> CreateRowReader(VirtualTextureUnpacker unpacker,
        GTSFlatTileInfo?[] rowTiles, CancellationToken ct)
    {
        return column => DecodeTileAsync(unpacker, rowTiles[column], ct);
    }

    /// <summary>
    ///     Decompresses one tile of a band, or returns <see langword="null" /> when the column holds no tile.
    /// </summary>
    /// <param name="unpacker">The unpacker the tile is read through.</param>
    /// <param name="tile">The tile to decompress, or <see langword="null" /> for an empty column.</param>
    /// <param name="ct">Cancels the decompression.</param>
    /// <returns>The decompressed tile, or <see langword="null" /> for an empty column.</returns>
    private static async Task<BC5Image?> DecodeTileAsync(VirtualTextureUnpacker unpacker, GTSFlatTileInfo? tile,
        CancellationToken ct)
    {
        if (tile is not { } cell) return null;
        ct.ThrowIfCancellationRequested();
        return await unpacker.Decompress(cell).ConfigureAwait(false);
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
    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
        Justification = "'Progress<T>' implements 'IProgress<T>.Report' explicitly, so the caller needs the interface.")]
    private static IProgress<(int Done, int Total)>? CreateProgress(IProgress<double>? progress)
    {
        if (progress is null) return null;
        return new Progress<(int Done, int Total)>(p =>
            progress.Report(p.Total == 0 ? 0 : p.Done * 100.0 / p.Total));
    }
}
