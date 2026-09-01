using System.Text;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

public sealed class VirtualTileSetExtractor : IDisposable
{
    private readonly TileCompressor _compressor = new();
    private readonly PageFileCache _pageFileCache;
    private readonly TileRangeCalculator _tileRanges;

    /// <summary>
    ///     Fully stream-based constructor: GTS metadata is read directly from <paramref name="gtsStream" />
    ///     without writing to disk; <paramref name="pageStreamProvider" /> supplies a stream for the GTP
    ///     page file of a given pageFileIndex (e.g. read from inside a PAK).
    /// </summary>
    public VirtualTileSetExtractor(Stream gtsStream, Func<int, Stream> pageStreamProvider)
    {
        using var reader = new BinaryReader(gtsStream, Encoding.UTF8, true);
        TileSet = new VirtualTileSet();
        TileSet.LoadFromStream(gtsStream, reader, false);
        PageFileNames = [.. TileSet.PageFileInfos.Select(f => f.FileName)];
        _pageFileCache = new PageFileCache(TileSet, pageStreamProvider);
        _tileRanges = new TileRangeCalculator(TileSet);
    }

    private VirtualTileSet TileSet { get; }

    public int LayerCount => TileSet.TileSetLayers.Length;

    /// <summary>GTP page file names referenced by the GTS, ordered by PageFileIndex.</summary>
    public IReadOnlyList<string> PageFileNames { get; }

    private int TileWidth => TileSet.Header.TileWidth - TileSet.Header.TileBorder * 2;

    private int TileHeight => TileSet.Header.TileHeight - TileSet.Header.TileBorder * 2;

    public void Dispose()
    {
        _pageFileCache.Dispose();
        TileSet.Dispose();
    }

    public List<FourCCTextureMeta> GetTextures()
    {
        return TileSet.FourCCMetadata.ExtractTextureMetadata();
    }

    private void ExtractToStream(int level, int layer, int minX, int minY, int maxX, int maxY,
        Stream output, IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        var (cols, rows) = GetTileRangeSize(minX, minY, maxX, maxY);

        using var writer = new DdsTitleWriter(output, cols, rows, TileWidth, TileHeight,
            (startX, y, colCount, strip) => StitchRow(level, layer, startX, y, colCount, strip));
        for (var row = 0; row < rows; row++)
        {
            writer.WriteRow(minX, minY + row, cols, ct);
            progress?.Report((row + 1, rows));
        }
    }

    /// <summary>Computes the tile grid dimensions of a range, rejecting empty ranges.</summary>
    private static (int Cols, int Rows) GetTileRangeSize(int minX, int minY, int maxX, int maxY)
    {
        var cols = maxX - minX + 1;
        var rows = maxY - minY + 1;
        if (cols <= 0 || rows <= 0) throw new ArgumentException("Empty tile range");
        return (cols, rows);
    }

    /// <summary>Stitches one horizontal band of tiles into <paramref name="strip" />, trimming tile borders.</summary>
    private void StitchRow(int level, int layer, int startX, int y, int cols, BC5Image strip)
    {
        Array.Clear(strip.Data);
        GTSFlatTileInfo tileInfo = default;
        for (var col = 0; col < cols; col++)
        {
            var tile = TryUnpackTile(level, layer, startX + col, y, ref tileInfo);
            // Skip the tile border and stitch into the strip row band via LSLib's BC5Image.CopyTo (4x4 blocks)
            tile?.CopyTo(strip, TileSet.Header.TileBorder, TileSet.Header.TileBorder,
                col * TileWidth, 0, TileWidth, TileHeight);
        }
    }

    /// <summary>
    ///     Decompresses the tile at (<paramref name="x" />, <paramref name="y" />) of the current level/layer,
    ///     or returns null when that position has no tile.
    /// </summary>
    private BC5Image? TryUnpackTile(int level, int layer, int x, int y, ref GTSFlatTileInfo tileInfo)
    {
        if (!TileSet.GetTileInfo(level, layer, x, y, ref tileInfo)) return null;
        var pageFile = _pageFileCache.Get(tileInfo.PageFileIndex);
        return pageFile.UnpackTileBc5(tileInfo.PageIndex, tileInfo.ChunkIndex, _compressor);
    }

    public bool ExtractTexture(int layer, FourCCTextureMeta tex, Stream output,
        IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        for (var level = 0; level < TileSet.TileSetLevels.Length; level++)
        {
            ct.ThrowIfCancellationRequested();
            if (!_tileRanges.TryGetTileRange(level, tex, out var minX, out var minY, out var maxX, out var maxY))
                continue;
            if (!_tileRanges.RegionExists(level, layer, minX, minY, maxX, maxY)) continue;

            ExtractToStream(level, layer, minX, minY, maxX, maxY, output, progress, ct);
            return true;
        }

        return false;
    }
}