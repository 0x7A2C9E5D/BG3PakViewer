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
    private readonly VirtualTextureTileRangeCalculator _tileRanges = new(file.TileSet);

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
        var ddsStream = new MemoryStream();
        var transferred = false;
        try
        {
            var extracted = await Task.Run(
                () => ExtractTexture(layer, file.Textures[index], ddsStream, CreateTileProgress(progress), ct), ct);
            if (!extracted) return null;

            ddsStream.Position = 0;
            transferred = true;
            return ddsStream;
        }
        finally
        {
            if (!transferred) await ddsStream.DisposeAsync();
        }
    }

    /// <summary>
    ///     Extracts <paramref name="layer" /> of <paramref name="tex" /> into <paramref name="output" />,
    ///     stopping at the first mip level whose tile region exists.
    /// </summary>
    /// <param name="layer">The index of the layer to extract.</param>
    /// <param name="tex">The texture to extract.</param>
    /// <param name="output">The stream the DDS data is written to.</param>
    /// <param name="progress">Reports how many tile rows have been written.</param>
    /// <param name="ct">Cancels the extraction.</param>
    /// <returns>
    ///     <see langword="true" /> when the layer was written to <paramref name="output" />; otherwise
    ///     <see langword="false" />, meaning the layer is empty at every mip level.
    /// </returns>
    private bool ExtractTexture(int layer, FourCCTextureMeta tex, Stream output,
        IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        for (var level = 0; level < file.TileSet.TileSetLevels.Length; level++)
        {
            ct.ThrowIfCancellationRequested();
            if (!_tileRanges.TryGetTileRange(level, tex, out var min, out var max))
                continue;
            if (!_tileRanges.RegionExists(level, layer, min, max)) continue;

            ExtractToStream(level, layer, min, max, output, progress, ct);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Extracts a region of a layer into a DDS stream.
    /// </summary>
    /// <param name="level">The index of the mip level the tiles are read from.</param>
    /// <param name="layer">The index of the layer to extract.</param>
    /// <param name="min">The first tile column and row of the region, inclusive.</param>
    /// <param name="max">The last tile column and row of the region, inclusive.</param>
    /// <param name="output">The stream the DDS data is written to.</param>
    /// <param name="progress">Reports how many tile rows have been written.</param>
    /// <param name="ct">Cancels the extraction.</param>
    private void ExtractToStream(int level, int layer, Point min, Point max, Stream output,
        IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        var size = VirtualTextureTileRangeCalculator.GetTileRangeSize(min, max);
        var tileSet = file.TileSet;

        // The unpacker keeps at most one-page file open and closes it when this region is done.
        using var unpacker = new VirtualTextureUnpacker((int)tileSet.Header.PageSize, tileSet.Header.TileWidth,
            tileSet.Header.TileHeight,
            pageFileIndex => pageStreamProvider(tileSet.PageFileInfos[pageFileIndex].FileName),
            tileSet.ParameterBlocks, _compressor);
        var writer = new VirtualTextureWriter(size,
            new Size(tileSet.EffectiveTileWidth, tileSet.EffectiveTileHeight),
            tileSet.Header.TileBorder, unpacker, output);
        writer.WriteHeader();
        var rowTiles = new GTSFlatTileInfo?[size.Width];
        for (var row = 0; row < size.Height; row++)
        {
            ResolveRow(level, layer, new Point(min.X, min.Y + row), rowTiles);
            writer.WriteRow(rowTiles, ct);
            progress?.Report((row + 1, size.Height));
        }
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