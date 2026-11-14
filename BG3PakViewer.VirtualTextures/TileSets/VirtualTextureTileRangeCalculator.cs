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
    private static (Point Origin, Size Size) GetTextureTileSpan(VirtualTileSet tileSet, FourCCTextureMeta tex)
    {
        return (new Point(tex.X / tileSet.EffectiveTileWidth, tex.Y / tileSet.EffectiveTileHeight),
            new Size(tex.Width / tileSet.EffectiveTileWidth, tex.Height / tileSet.EffectiveTileHeight));
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
        // A tile of the coarser level covers lv tiles of level 0, so the span covers the coarser tiles that
        // contain its first and last level-0 tiles. Rounding the start-up instead would drop the first
        // column and row whenever the texture does not start on a tile boundary of that level.
        var lv = 1 << level;
        return (new Point(origin.X / lv, origin.Y / lv),
            new Point((origin.X + span.Width - 1) / lv, (origin.Y + span.Height - 1) / lv));
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