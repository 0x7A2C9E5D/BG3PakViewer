using System.Drawing;
using BG3PakViewer.VirtualTextures.PageFiles;
using BG3PakViewer.VirtualTextures.TileSets;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Extracts one layer of a texture of a parsed tile set into a seekable DDS stream.
/// </summary>
/// <remarks>
///     The layer is written from the first mip level whose tile region exists, one tile band per row. The
///     tiles are looked up in the tile set here, read through the page stream provider, and only the page
///     file a band is reading stays open. The extractor owns the tile set it was created with, so
///     disposing it releases that tile set, and it keeps the decompressor every band of that tile set
///     needs.
/// </remarks>
/// <param name="file">The parsed tile set the texture belongs to; disposing the extractor disposes it.</param>
/// <param name="pageStreamProvider">Opens the page file with the given name, e.g. from inside a PAK.</param>
internal sealed class VirtualTextureLayerExtractor(VirtualTileSetFile file, Func<string, Stream> pageStreamProvider)
    : IDisposable
{
    private readonly TileCompressor _compressor = new();

    /// <summary>
    ///     Releases the parsed tile set this extractor was created with.
    /// </summary>
    public void Dispose()
    {
        file.Dispose();
    }

    /// <summary>
    ///     Extracts <paramref name="layer" /> of the texture at <paramref name="index" /> into a seekable
    ///     DDS stream, reporting progress in percent.
    /// </summary>
    /// <param name="index">The index of the texture in the tile set's texture list.</param>
    /// <param name="layer">The index of the layer to extract.</param>
    /// <param name="progress">Reports the extraction progress, in percent.</param>
    /// <param name="ct">Cancels the extraction.</param>
    /// <returns>
    ///     A seekable DDS stream, or <see langword="null" /> when the layer contains no data.
    /// </returns>
    public async Task<Stream?> ExtractAsync(int index, int layer, IProgress<double>? progress, CancellationToken ct)
    {
        return await Task.Run(
            () => ExtractTexture(layer, file.Textures[index], CreateTileProgress(progress), ct), ct);
    }

    /// <summary>
    ///     Extracts <paramref name="layer" /> of <paramref name="tex" /> into a new DDS stream, stopping
    ///     at the first mip level whose tile region exists.
    /// </summary>
    /// <param name="layer">The index of the layer to extract.</param>
    /// <param name="tex">The texture to extract.</param>
    /// <param name="progress">Reports how many tile rows have been written.</param>
    /// <param name="ct">Cancels the extraction.</param>
    /// <returns>
    ///     A seekable DDS stream, or <see langword="null" /> when the layer is empty at every mip level.
    /// </returns>
    private Stream? ExtractTexture(int layer, FourCCTextureMeta tex,
        IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        for (var level = 0; level < file.TileSet.TileSetLevels.Length; level++)
        {
            ct.ThrowIfCancellationRequested();
            if (VirtualTextureTileRangeCalculator.GetTileRange(file.TileSet, level, tex) is not { } range)
                continue;
            var (min, max) = range;
            if (!VirtualTextureTileRangeCalculator.RegionExists(file.TileSet, level, layer, min, max)) continue;

            return ExtractToStream(level, layer, min, max, progress, ct);
        }

        return null;
    }

    /// <summary>
    ///     Extracts a region of a layer into a new DDS stream.
    /// </summary>
    /// <param name="level">The index of the mip level the tiles are read from.</param>
    /// <param name="layer">The index of the layer to extract.</param>
    /// <param name="min">The first tile column and row of the region, inclusive.</param>
    /// <param name="max">The last tile column and row of the region, inclusive.</param>
    /// <param name="progress">Reports how many tile rows have been written.</param>
    /// <param name="ct">Cancels the extraction.</param>
    /// <returns>A seekable DDS stream, positioned at its start.</returns>
    private MemoryStream ExtractToStream(int level, int layer, Point min, Point max,
        IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        var size = VirtualTextureTileRangeCalculator.GetTileRangeSize(min, max);
        var tileSet = file.TileSet;
        var dds = new MemoryStream();

        // The unpacker keeps at most one-page file open and closes it when this region is done.
        var tile = tileSet.TileSize;
        var decoder = new VirtualTexturePageChunkDecoder(tile, tileSet.ParameterBlocks, _compressor);
        using var unpacker = new VirtualTextureUnpacker(tileSet.PageSize,
            pageFileIndex => pageStreamProvider(tileSet.PageFileInfos[pageFileIndex].FileName), decoder);
        var writer = new VirtualTextureWriter(size, tile, tileSet.TileBorder, unpacker, dds);
        writer.WriteHeader();
        var rowTiles = new GTSFlatTileInfo?[size.Width];
        for (var row = 0; row < size.Height; row++)
        {
            ct.ThrowIfCancellationRequested();
            ResolveRow(level, layer, new Point(min.X, min.Y + row), rowTiles);
            writer.WriteRow(rowTiles);
            progress?.Report((row + 1, size.Height));
        }

        dds.Position = 0;
        return dds;
    }

    /// <summary>
    ///     Looks up the tiles of one band in the tile set, leaving a column empty when the tile set has no
    ///     tile there.
    /// </summary>
    /// <param name="level">The index of the mip level the tiles are read from.</param>
    /// <param name="layer">The index of the layer the tiles are read from.</param>
    /// <param name="origin">The tile column and row the band starts at.</param>
    /// <param name="rowTiles">The buffer the band is resolved into, one entry per column.</param>
    private void ResolveRow(int level, int layer, Point origin, Span<GTSFlatTileInfo?> rowTiles)
    {
        for (var col = 0; col < rowTiles.Length; col++)
        {
            GTSFlatTileInfo tile = default;
            rowTiles[col] = file.TileSet.GetTileInfo(level, layer, origin.X + col, origin.Y, ref tile)
                ? tile
                : null;
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
    private static Progress<(int Done, int Total)>? CreateTileProgress(IProgress<double>? progress)
    {
        if (progress is null) return null;
        return new Progress<(int Done, int Total)>(p =>
            progress.Report(p.Total == 0 ? 0 : p.Done * 100.0 / p.Total));
    }
}