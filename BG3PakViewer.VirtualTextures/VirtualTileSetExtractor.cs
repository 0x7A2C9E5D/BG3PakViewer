using System.Text;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

public sealed class VirtualTileSetExtractor : IDisposable
{
    private readonly TexturePageCache _texturePageCache;
    private readonly TileRangeCalculator _tileRanges;
    private readonly TextureUnpacker _unpacker;

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
        TextureNames = [.. TileSet.PageFileInfos.Select(f => f.FileName)];
        _texturePageCache = new TexturePageCache(TileSet, pageStreamProvider);
        _tileRanges = new TileRangeCalculator(TileSet);
        _unpacker = new TextureUnpacker(TileSet, _texturePageCache);
    }

    private VirtualTileSet TileSet { get; }

    public int LayerCount => TileSet.TileSetLayers.Length;

    /// <summary>GTP page file names referenced by the GTS, ordered by PageFileIndex.</summary>
    public IReadOnlyList<string> TextureNames { get; }

    public void Dispose()
    {
        _texturePageCache.Dispose();
        TileSet.Dispose();
    }

    public List<FourCCTextureMeta> GetTextures()
    {
        return TileSet.FourCCMetadata.ExtractTextureMetadata();
    }

    private void ExtractToStream(int level, int layer, int minX, int minY, int maxX, int maxY,
        Stream output, IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        var (cols, rows) = TileRangeCalculator.GetTileRangeSize(minX, minY, maxX, maxY);

        using var writer = new TextureWriter(output, cols, rows, _tileRanges.TileWidth, _tileRanges.TileHeight,
            (startX, y, colCount, strip) => _unpacker.StitchRow(level, layer, startX, y, colCount, strip));
        for (var row = 0; row < rows; row++)
        {
            writer.WriteRow(minX, minY + row, cols, ct);
            progress?.Report((row + 1, rows));
        }
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