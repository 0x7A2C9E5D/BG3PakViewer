using System.Text;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

public sealed class StreamingTileSetExtractor : IDisposable
{
    private readonly TileCompressor _compressor = new();

    private VirtualTileSet TileSet { get; }

    private PageFileCache Cache { get; }
    
    public int LayerCount => TileSet.TileSetLayers.Length;
    
    public int LevelCount => TileSet.TileSetLevels.Length;

    /// <summary>GTP page file names referenced by the GTS, ordered by PageFileIndex.</summary>
    public IReadOnlyList<string> PageFileNames { get; }

    /// <summary>
    /// Fully stream-based constructor: GTS metadata is read directly from <paramref name="gtsStream"/>
    /// without writing to disk; <paramref name="pageStreamProvider"/> supplies a stream for the GTP
    /// page file of a given pageFileIndex (e.g. read from inside a PAK).
    /// </summary>
    public StreamingTileSetExtractor(Stream gtsStream, Func<int, Stream> pageStreamProvider)
    {
        using var reader = new BinaryReader(gtsStream, Encoding.UTF8, leaveOpen: true);
        TileSet = new VirtualTileSet();
        TileSet.LoadFromStream(gtsStream, reader, false);
        PageFileNames = [.. TileSet.PageFileInfos.Select(f => f.FileName)];
        Cache = new PageFileCache(TileSet, pageStreamProvider);
    }

    public List<FourCCTextureMeta> GetTextures() => TileSet.FourCCMetadata.ExtractTextureMetadata();

    private bool TryGetTileRange(int level, FourCCTextureMeta tex,
        out int minX, out int minY, out int maxX, out int maxY)
    {
        var tlW = TileSet.Header.TileWidth - TileSet.Header.TileBorder * 2;
        var tlH = TileSet.Header.TileHeight - TileSet.Header.TileBorder * 2;
        var tX = tex.X / tlW;
        var tY = tex.Y / tlH;
        var tW = tex.Width / tlW;
        var tH = tex.Height / tlH;
        var lv = 1 << level;

        minX = (tX / lv) + ((tX % lv) > 0 ? 1 : 0);
        minY = (tY / lv) + ((tY % lv) > 0 ? 1 : 0);
        maxX = ((tX + tW) / lv) + (((tX + tW) % lv) > 0 ? 1 : 0) - 1;
        maxY = ((tY + tH) / lv) + (((tY + tH) % lv) > 0 ? 1 : 0) - 1;

        return maxX >= minX && maxY >= minY;
    }

    private bool RegionExists(int level, int layer, int minX, int minY, int maxX, int maxY)
    {
        GTSFlatTileInfo tile = default;
        for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
                if (!TileSet.GetTileInfo(level, layer, x, y, ref tile)) return false;
        return true;
    }

    private void ExtractToStream(int level, int layer, int minX, int minY, int maxX, int maxY,
        Stream output, IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        var hdr = TileSet.Header;
        var tileW = hdr.TileWidth - hdr.TileBorder * 2;
        var tileH = hdr.TileHeight - hdr.TileBorder * 2;
        var cols = maxX - minX + 1;
        var rows = maxY - minY + 1;
        if (cols <= 0 || rows <= 0) throw new ArgumentException("Empty tile range");

        var width = cols * tileW;
        var height = rows * tileH;

        var header = new DDSHeader
        {
            dwMagic = DDSHeader.DDSMagic,
            dwSize = DDSHeader.HeaderSize,
            dwFlags = 0x1007,
            dwWidth = (uint)width,
            dwHeight = (uint)height,
            dwPitchOrLinearSize = (uint)(width * height),
            dwDepth = 1,
            dwMipMapCount = 1,
            dwPFSize = 32,
            dwPFFlags = 0x04,
            dwFourCC = DDSHeader.FourCC_DXT5,
            dwCaps = 0x1000
        };
        using var bw = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        BinUtils.WriteStruct(bw, ref header);

        var strip = new BC5Image(width, tileH);
        GTSFlatTileInfo tileInfo = default;

        for (var row = 0; row < rows; row++)
        {
            ct.ThrowIfCancellationRequested();
            Array.Clear(strip.Data);

            for (var col = 0; col < cols; col++)
            {
                if (!TileSet.GetTileInfo(level, layer, minX + col, minY + row, ref tileInfo)) continue;

                var pageFile = Cache.Get(tileInfo.PageFileIndex);
                var img = pageFile.UnpackTileBc5(tileInfo.PageIndex, tileInfo.ChunkIndex, _compressor);
                // Skip the tile border and stitch into the strip row band via LSLib's BC5Image.CopyTo (4x4 blocks)
                img.CopyTo(strip, hdr.TileBorder, hdr.TileBorder,
                    col * tileW, 0, tileW, tileH);
            }

            output.Write(strip.Data, 0, strip.Data.Length);
            progress?.Report((row + 1, rows));
        }
    }

    public bool ExtractTexture(int layer, FourCCTextureMeta tex, Stream output,
        IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        for (var level = 0; level < TileSet.TileSetLevels.Length; level++)
        {
            ct.ThrowIfCancellationRequested();
            if (!TryGetTileRange(level, tex, out var minX, out var minY, out var maxX, out var maxY)) continue;
            if (!RegionExists(level, layer, minX, minY, maxX, maxY)) continue;

            ExtractToStream(level, layer, minX, minY, maxX, maxY, output, progress, ct);
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        Cache.Dispose();
        TileSet.Dispose();
    }
}
