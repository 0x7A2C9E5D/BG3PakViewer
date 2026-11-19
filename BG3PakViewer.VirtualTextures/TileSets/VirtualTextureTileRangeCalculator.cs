using System.Drawing;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.TileSets;

/// <summary>
///     Computes tile-grid coverage for virtual textures: maps a texture's pixel span onto the
///     tile range of a mip level and validates that a region is fully present in a layer.
/// </summary>
/// <remarks>
///     The calculator holds no tile set: every call receives the tile set it works on, so the same
///     rules serve any tile set without the calculator having to be rebuilt for each one.
/// </remarks>
internal static class VirtualTextureTileRangeCalculator
{
    /// <summary>
    ///     Gets the tile range size (columns, rows) for a given tile range.
    /// </summary>
    /// <param name="min">The first tile column and row of the region, inclusive.</param>
    /// <param name="max">The last tile column and row of the region, inclusive.</param>
    /// <returns>How many tiles the region is wide and tall.</returns>
    /// <exception cref="ArgumentException">The region is empty.</exception>
    public static Size GetTileRangeSize(Point min, Point max)
    {
        var cols = max.X - min.X + 1;
        var rows = max.Y - min.Y + 1;
        if (cols <= 0 || rows <= 0) throw new ArgumentException("Empty tile range");
        return new Size(cols, rows);
    }

    /// <summary>
    ///     Gets the tile range for a texture at a given mip level.
    /// </summary>
    /// <param name="tileSet">The tile set the texture belongs to.</param>
    /// <param name="level">The index of the mip level.</param>
    /// <param name="tex">The texture metadata, which places the texture on the level 0 tile grid.</param>
    /// <returns>
    ///     The first and last tile column and row covering the texture at that level, both ends inclusive
    ///     and clipped to the tile grid of the level; <see langword="null" /> when no tile remains.
    /// </returns>
    public static (Point Min, Point Max)? GetTileRange(VirtualTileSet tileSet, int level,
        FourCCTextureMeta tex)
    {
        var (origin, span) = GetTextureTileSpan(tileSet, tex);
        var (min, max) = ToLevelRange(origin, span, level);

        // GetTileInfo indexes the flat tile array of this level without bounds checks,
        // so clamp the range to the level's actual tile grid.
        max = ClampToLevelGrid(tileSet, level, max);

        return max.X >= min.X && max.Y >= min.Y ? (min, max) : null;
    }

    /// <summary>
    ///     Gets the tile span for a texture at level 0.
    /// </summary>
    /// <param name="tileSet">The tile set the texture belongs to.</param>
    /// <param name="tex">The texture metadata, in pixels.</param>
    /// <returns>
    ///     The texture position and size on the level 0 tile grid, in tiles.
    /// </returns>
    /// <remarks>
    ///     The size is rounded up because a texture that does not end on a tile boundary still has its
    ///     last pixels stored: rounding it down drops that column and row from every extraction, which
    ///     is what the tile data shows (the content of a 1076 px wide texture runs to 1076, not to 1024).
    ///     The position is not rounded up, because the textures start on tile boundaries.
    /// </remarks>
    private static (Point Origin, Size Size) GetTextureTileSpan(VirtualTileSet tileSet, FourCCTextureMeta tex)
    {
        var tileWidth = tileSet.EffectiveTileWidth;
        var tileHeight = tileSet.EffectiveTileHeight;
        return (new Point(tex.X / tileWidth, tex.Y / tileHeight),
            new Size(DivideCeiling(tex.Width, tileWidth), DivideCeiling(tex.Height, tileHeight)));
    }

    /// <summary>
    ///     Divides a pixel size by the size of one tile, rounding up to the whole tiles it covers.
    /// </summary>
    /// <param name="value">The value to divide.</param>
    /// <param name="divisor">The tile size to divide by.</param>
    /// <returns>The number of tiles needed to hold <paramref name="value" />.</returns>
    private static int DivideCeiling(int value, int divisor)
    {
        return value / divisor + (value % divisor > 0 ? 1 : 0);
    }

    /// <summary>
    ///     Converts a texture span to a tile range at a given mip level.
    /// </summary>
    /// <param name="origin">The first tile column and row on level 0.</param>
    /// <param name="span">How many tiles wide and tall the texture is on level 0.</param>
    /// <param name="level">The index of the mip level to convert to.</param>
    /// <returns>The tile range covering the span at that level, both ends inclusive.</returns>
    private static (Point Min, Point Max) ToLevelRange(Point origin, Size span, int level)
    {
        var lv = 1 << level;
        return (new Point(DivideCeiling(origin.X, lv), DivideCeiling(origin.Y, lv)),
            new Point(DivideCeiling(origin.X + span.Width, lv) - 1,
                DivideCeiling(origin.Y + span.Height, lv) - 1));
    }

    /// <summary>
    ///     Clips the last tile of a range to the tile grid of a given mip level.
    /// </summary>
    /// <param name="tileSet">The tile set whose mip levels the range is clipped to.</param>
    /// <param name="level">The index of the mip level whose tile grid the range is clipped to.</param>
    /// <param name="max">The last tile column and row, lowered to the grid when they overflow.</param>
    /// <returns>The last tile column and row, inside the tile grid of that level.</returns>
    private static Point ClampToLevelGrid(VirtualTileSet tileSet, int level, Point max)
    {
        var levelWidth = (int)tileSet.TileSetLevels[level].Width;
        var levelHeight = (int)tileSet.TileSetLevels[level].Height;
        return new Point(Math.Min(max.X, levelWidth - 1), Math.Min(max.Y, levelHeight - 1));
    }

    /// <summary>
    ///     Checks if a region exists in a given layer.
    /// </summary>
    /// <param name="tileSet">The tile set the region is looked up in.</param>
    /// <param name="level">The index of the mip level the region lives at.</param>
    /// <param name="layer">The index of the layer the region must exist in.</param>
    /// <param name="min">The first tile column and row of the region, inclusive.</param>
    /// <param name="max">The last tile column and row of the region, inclusive.</param>
    /// <returns>
    ///     <see langword="true" /> when every tile of the region exists in that layer; otherwise
    ///     <see langword="false" />, meaning the layer leaves part of the region empty.
    /// </returns>
    public static bool RegionExists(VirtualTileSet tileSet, int level, int layer, Point min, Point max)
    {
        GTSFlatTileInfo tile = default;
        for (var y = min.Y; y <= max.Y; y++)
            for (var x = min.X; x <= max.X; x++)
                if (!tileSet.GetTileInfo(level, layer, x, y, ref tile))
                    return false;
        return true;
    }
}
