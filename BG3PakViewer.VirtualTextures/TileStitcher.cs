using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

/// <summary>
///     Decompresses individual tiles from GTP page files and stitches one horizontal band
///     of tiles into a reusable strip buffer, trimming tile borders.
/// </summary>
public sealed class TileStitcher(VirtualTileSet tileSet, PageFileCache pageFileCache)
{
    private readonly TileCompressor _compressor = new();

    private int TileWidth => tileSet.Header.TileWidth - tileSet.Header.TileBorder * 2;

    private int TileHeight => tileSet.Header.TileHeight - tileSet.Header.TileBorder * 2;

    /// <summary>Stitches one horizontal band of tiles into <paramref name="strip" />, trimming tile borders.</summary>
    public void StitchRow(int level, int layer, int startX, int y, int cols, BC5Image strip)
    {
        Array.Clear(strip.Data);
        GTSFlatTileInfo tileInfo = default;
        for (var col = 0; col < cols; col++)
        {
            var tile = TryUnpackTile(level, layer, startX + col, y, ref tileInfo);
            // Skip the tile border and stitch into the strip row band via LSLib's BC5Image.CopyTo (4x4 blocks)
            tile?.CopyTo(strip, tileSet.Header.TileBorder, tileSet.Header.TileBorder,
                col * TileWidth, 0, TileWidth, TileHeight);
        }
    }

    /// <summary>
    ///     Decompresses the tile at (<paramref name="x" />, <paramref name="y" />) of the current level/layer,
    ///     or returns null when that position has no tile.
    /// </summary>
    private BC5Image? TryUnpackTile(int level, int layer, int x, int y, ref GTSFlatTileInfo tileInfo)
    {
        if (!tileSet.GetTileInfo(level, layer, x, y, ref tileInfo)) return null;
        var pageFile = pageFileCache.Get(tileInfo.PageFileIndex);
        return pageFile.UnpackTileBc5(tileInfo.PageIndex, tileInfo.ChunkIndex, _compressor);
    }
}
