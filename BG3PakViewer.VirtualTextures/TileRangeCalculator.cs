using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

/// <summary>
///     Computes tile-grid coverage for virtual textures: maps a texture's pixel span onto the
///     tile range of a mip level and validates that a region is fully present in a layer.
/// </summary>
public sealed class TileRangeCalculator(VirtualTileSet tileSet)
{
    /// <summary>Effective tile width after trimming the border.</summary>
    public int TileWidth => tileSet.Header.TileWidth - tileSet.Header.TileBorder * 2;

    /// <summary>Effective tile height after trimming the border.</summary>
    public int TileHeight => tileSet.Header.TileHeight - tileSet.Header.TileBorder * 2;

    /// <summary>Computes the tile grid dimensions of a range, rejecting empty ranges.</summary>
    public static (int Cols, int Rows) GetTileRangeSize(int minX, int minY, int maxX, int maxY)
    {
        var cols = maxX - minX + 1;
        var rows = maxY - minY + 1;
        if (cols <= 0 || rows <= 0) throw new ArgumentException("Empty tile range");
        return (cols, rows);
    }

    public bool TryGetTileRange(int level, FourCCTextureMeta tex,
        out int minX, out int minY, out int maxX, out int maxY)
    {
        var (x, y, w, h) = GetTextureTileSpan(tex);
        (minX, minY, maxX, maxY) = ToLevelRange(x, y, w, h, level);

        // GetTileInfo indexes the flat tile array of this level without bounds checks,
        // so clamp the range to the level's actual tile grid.
        ClampToLevelGrid(level, ref maxX, ref maxY);

        return maxX >= minX && maxY >= minY;
    }

    /// <summary>Returns the tile-grid span (start x/y and tile counts) covered by a texture at level 0.</summary>
    private (int X, int Y, int Width, int Height) GetTextureTileSpan(FourCCTextureMeta tex)
    {
        return (tex.X / TileWidth, tex.Y / TileHeight,
            tex.Width / TileWidth, tex.Height / TileHeight);
    }

    /// <summary>Scales a level-0 tile span down to the coarser grid of the given mip level.</summary>
    private static (int MinX, int MinY, int MaxX, int MaxY) ToLevelRange(
        int x, int y, int width, int height, int level)
    {
        var lv = 1 << level;
        return (DivideCeiling(x, lv), DivideCeiling(y, lv),
            DivideCeiling(x + width, lv) - 1, DivideCeiling(y + height, lv) - 1);
    }

    private static int DivideCeiling(int value, int divisor)
    {
        return value / divisor + (value % divisor > 0 ? 1 : 0);
    }

    private void ClampToLevelGrid(int level, ref int maxX, ref int maxY)
    {
        var levelWidth = (int)tileSet.TileSetLevels[level].Width;
        var levelHeight = (int)tileSet.TileSetLevels[level].Height;
        if (maxX > levelWidth - 1) maxX = levelWidth - 1;
        if (maxY > levelHeight - 1) maxY = levelHeight - 1;
    }

    public bool RegionExists(int level, int layer, int minX, int minY, int maxX, int maxY)
    {
        GTSFlatTileInfo tile = default;
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
            if (!tileSet.GetTileInfo(level, layer, x, y, ref tile))
                return false;
        return true;
    }
}