using System.Text;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Fully stream-based virtual texture extractor: loads a tile set directly from a stream (no temp
///     files), exposes its layers and texture metadata, and extracts a selected layer into a DDS stream
///     that can be decoded by <c>ImageLoader.LoadAsync</c>.
/// </summary>
/// <remarks>
///     Parsing needs nothing but the tile set stream. The GTP page files are separate files whose names
///     the tile set lists, so they are opened on demand through the page cache that
///     <see cref="CreatePageCache" /> builds once the caller knows where the pages come from.
/// </remarks>
internal sealed class VirtualTextureLoader : IDisposable
{
    private readonly VirtualTextureTileRangeCalculator _tileRanges;

    /// <summary>
    ///     Reads the tile set metadata directly from <paramref name="stream" />, without opening any page
    ///     file.
    /// </summary>
    /// <param name="stream">The tile set stream, which is read again during extraction.</param>
    public VirtualTextureLoader(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        TileSet = new VirtualTileSet();
        TileSet.LoadFromStream(stream, reader, false);
        _tileRanges = new VirtualTextureTileRangeCalculator(TileSet);
    }

    private VirtualTileSet TileSet { get; }

    /// <summary>
    ///     The number of layers in the virtual texture.
    /// </summary>
    internal int LayerCount => TileSet.TileSetLayers.Length;

    /// <summary>
    ///     Releases the parsed tile set.
    /// </summary>
    public void Dispose()
    {
        TileSet.Dispose();
    }

    /// <summary>
    ///     Gets the metadata for all textures in the virtual texture.
    /// </summary>
    /// <returns>
    ///     The textures in tile set order, which is the order the layer metadata is indexed in.
    /// </returns>
    internal List<FourCCTextureMeta> GetTextures()
    {
        return TileSet.FourCCMetadata.ExtractTextureMetadata();
    }

    /// <summary>
    ///     Creates the cache that opens the GTP page files of this tile set on demand.
    /// </summary>
    /// <param name="pageStreamProvider">Opens the page file with the given name, e.g. from inside a PAK.</param>
    /// <returns>The page cache, which the caller owns and disposes together with the handle.</returns>
    internal VirtualTexturePageCache CreatePageCache(Func<string, Stream> pageStreamProvider)
    {
        return new VirtualTexturePageCache(TileSet, pageStreamProvider);
    }

    /// <summary>
    ///     Extracts <paramref name="layer" /> of <paramref name="meta" /> into a seekable DDS stream,
    ///     opening the GTP pages it needs through <paramref name="pages" /> and reporting progress in
    ///     percent.
    /// </summary>
    /// <param name="meta">The texture to extract, as returned by <see cref="GetTextures" />.</param>
    /// <param name="layer">The index of the layer to extract.</param>
    /// <param name="pages">The page cache that opens the GTP page files the layer needs.</param>
    /// <param name="progress">Reports the extraction progress, in percent.</param>
    /// <param name="ct">Cancels the extraction.</param>
    /// <returns>
    ///     A seekable DDS stream, or <see langword="null" /> when the layer contains no data.
    /// </returns>
    public async Task<Stream?> ExtractAsync(FourCCTextureMeta meta, int layer, VirtualTexturePageCache pages,
        IProgress<double>? progress, CancellationToken ct)
    {
        var ddsStream = new MemoryStream();
        var transferred = false;
        try
        {
            var extracted = await Task.Run(
                () => ExtractTexture(layer, meta, ddsStream, pages, CreateTileProgress(progress), ct), ct);
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
    /// <param name="pages">The page cache that opens the GTP page files the layer needs.</param>
    /// <param name="progress">Reports how many tile rows have been written.</param>
    /// <param name="ct">Cancels the extraction.</param>
    /// <returns>
    ///     <see langword="true" /> when the layer was written to <paramref name="output" />; otherwise
    ///     <see langword="false" />, meaning the layer is empty at every mip level.
    /// </returns>
    private bool ExtractTexture(int layer, FourCCTextureMeta tex, Stream output, VirtualTexturePageCache pages,
        IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        for (var level = 0; level < TileSet.TileSetLevels.Length; level++)
        {
            ct.ThrowIfCancellationRequested();
            if (!_tileRanges.TryGetTileRange(level, tex, out var minX, out var minY, out var maxX, out var maxY))
                continue;
            if (!_tileRanges.RegionExists(level, layer, minX, minY, maxX, maxY)) continue;

            ExtractToStream(level, layer, minX, minY, maxX, maxY, output, pages, progress, ct);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Extracts a region of a layer into a DDS stream.
    /// </summary>
    /// <param name="level">The index of the mip level the tiles are read from.</param>
    /// <param name="layer">The index of the layer to extract.</param>
    /// <param name="minX">The first tile column of the region, inclusive.</param>
    /// <param name="minY">The first tile row of the region, inclusive.</param>
    /// <param name="maxX">The last tile column of the region, inclusive.</param>
    /// <param name="maxY">The last tile row of the region, inclusive.</param>
    /// <param name="output">The stream the DDS data is written to.</param>
    /// <param name="pages">The page cache that opens the GTP page files the region needs.</param>
    /// <param name="progress">Reports how many tile rows have been written.</param>
    /// <param name="ct">Cancels the extraction.</param>
    private void ExtractToStream(int level, int layer, int minX, int minY, int maxX, int maxY,
        Stream output, VirtualTexturePageCache pages, IProgress<(int Done, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        var (cols, rows) = VirtualTextureTileRangeCalculator.GetTileRangeSize(minX, minY, maxX, maxY);

        using var writer = new VirtualTextureWriter(output, cols, rows, TileSet.EffectiveTileWidth,
            TileSet.EffectiveTileHeight, new VirtualTextureUnpacker(TileSet, pages), level, layer);
        for (var row = 0; row < rows; row++)
        {
            writer.WriteRow(minX, minY + row, cols, ct);
            progress?.Report((row + 1, rows));
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