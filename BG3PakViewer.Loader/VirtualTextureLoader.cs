using System.IO;
using System.Text;
using BG3PakViewer.VirtualTextures;
using LSLib.VirtualTextures;

namespace BG3PakViewer.Loader;

/// <summary>
///     Fully stream-based virtual texture extractor: loads a GTS directly from a stream (no temp files),
///     exposes its layers and texture metadata, and extracts a selected layer into a DDS stream that can
///     be decoded by <c>ImageLoader.DecodeDdsAsync</c>. GTP page files are opened lazily per pageFileIndex
///     via a stream provider delegate (e.g. read from inside a PAK).
/// </summary>
public sealed class VirtualTextureLoader : IDisposable
{
    private readonly TexturePageCache _texturePageCache;
    private readonly TileRangeCalculator _tileRanges;
    private readonly TextureUnpacker _unpacker;

    /// <summary>
    ///     Fully stream-based constructor: GTS metadata is read directly from <paramref name="gtsStream" />
    ///     without writing to disk; <paramref name="pageStreamProvider" /> supplies a stream for the GTP
    ///     page file of a given pageFileIndex (e.g. read from inside a PAK).
    /// </summary>
    public VirtualTextureLoader(Stream gtsStream, Func<int, Stream> pageStreamProvider)
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

    public List<FourCCTextureMeta> GetTextures()
    {
        return TileSet.FourCCMetadata.ExtractTextureMetadata();
    }

    /// <summary>
    ///     Extracts <paramref name="layer" /> of <paramref name="meta" /> into a seekable DDS stream,
    ///     reporting progress in percent; returns null when the layer contains no data.
    /// </summary>
    public async Task<Stream?> ExtractAsync(FourCCTextureMeta meta, int layer,
        IProgress<double>? progress, CancellationToken ct)
    {
        var ddsStream = new MemoryStream();
        var transferred = false;
        try
        {
            var extracted = await Task.Run(
                () => ExtractTexture(layer, meta, ddsStream, CreateTileProgress(progress), ct), ct);
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

    public void Dispose()
    {
        _texturePageCache.Dispose();
        TileSet.Dispose();
    }

    private bool ExtractTexture(int layer, FourCCTextureMeta tex, Stream output,
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

    private static Progress<(int Done, int Total)>? CreateTileProgress(IProgress<double>? progress)
    {
        if (progress is null) return null;
        return new Progress<(int Done, int Total)>(p =>
            progress.Report(p.Total == 0 ? 0 : p.Done * 100.0 / p.Total));
    }
}
