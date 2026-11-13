using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Computes tile-grid coverage for virtual textures: maps a texture's pixel span onto the
///     tile range of a mip level and validates that a region is fully present in a layer.
/// </summary>
internal sealed class VirtualTextureTileRangeCalculator(VirtualTileSet tileSet)
{
    /// <summary>
    ///     Gets the tile range size (cols, rows) for a given tile range.
    /// </summary>
    /// <param name="minX">The first tile column of the region, inclusive.</param>
    /// <param name="minY">The first tile row of the region, inclusive.</param>
    /// <param name="maxX">The last tile column of the region, inclusive.</param>
    /// <param name="maxY">The last tile row of the region, inclusive.</param>
    /// <returns>How many tiles the region is wide and tall.</returns>
    /// <exception cref="ArgumentException">The region is empty.</exception>
    public static (int Cols, int Rows) GetTileRangeSize(int minX, int minY, int maxX, int maxY)
    {
        var cols = maxX - minX + 1;
        var rows = maxY - minY + 1;
        if (cols <= 0 || rows <= 0) throw new ArgumentException("Empty tile range");
        return (cols, rows);
    }

    /// <summary>
    ///     Gets the tile range for a texture at a given mip level.
    /// </summary>
    /// <param name="level">The index of the mip level.</param>
    /// <param name="tex">The texture metadata, which places the texture on the level 0 tile grid.</param>
    /// <param name="minX">The first tile column covering the texture at that level, inclusive.</param>
    /// <param name="minY">The first tile row covering the texture at that level, inclusive.</param>
    /// <param name="maxX">The last tile column, clipped to the tile grid of the level.</param>
    /// <param name="maxY">The last tile row, clipped to the tile grid of the level.</param>
    /// <returns>
    ///     <see langword="true" /> when at least one tile of the texture remains after clipping to the
    ///     grid of that level.
    /// </returns>
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

    /// <summary>
    ///     Gets the tile span for a texture at level 0.
    /// </summary>
    /// <param name="tex">The texture metadata, in pixels.</param>
    /// <returns>The texture position and size on the level 0 tile grid, in tiles.</returns>
    private (int X, int Y, int Width, int Height) GetTextureTileSpan(FourCCTextureMeta tex)
    {
        return (tex.X / tileSet.EffectiveTileWidth, tex.Y / tileSet.EffectiveTileHeight,
            tex.Width / tileSet.EffectiveTileWidth, tex.Height / tileSet.EffectiveTileHeight);
    }

    /// <summary>
    ///     Converts a texture span to a tile range at a given mip level.
    /// </summary>
    /// <param name="x">The first tile column on level 0.</param>
    /// <param name="y">The first tile row on level 0.</param>
    /// <param name="width">How many tiles wide the texture is on level 0.</param>
    /// <param name="height">How many tiles tall the texture is on level 0.</param>
    /// <param name="level">The index of the mip level to convert to.</param>
    /// <returns>The tile range covering the span at that level, both ends inclusive.</returns>
    private static (int MinX, int MinY, int MaxX, int MaxY) ToLevelRange(
        int x, int y, int width, int height, int level)
    {
        var lv = 1 << level;
        return (DivideCeiling(x, lv), DivideCeiling(y, lv),
            DivideCeiling(x + width, lv) - 1, DivideCeiling(y + height, lv) - 1);
    }

    /// <summary>
    ///     Divides a value by a divisor and rounds up.
    /// </summary>
    /// <param name="value">The value to divide.</param>
    /// <param name="divisor">The divisor, greater than zero.</param>
    /// <returns>The quotient, rounded up.</returns>
    private static int DivideCeiling(int value, int divisor)
    {
        return value / divisor + (value % divisor > 0 ? 1 : 0);
    }

    /// <summary>
    ///     Clips a tile range to the tile grid of a given mip level.
    /// </summary>
    /// <param name="level">The index of the mip level whose tile grid the range is clipped to.</param>
    /// <param name="maxX">The last tile column, lowered to the grid width when it overflows.</param>
    /// <param name="maxY">The last tile row, lowered to the grid height when it overflows.</param>
    private void ClampToLevelGrid(int level, ref int maxX, ref int maxY)
    {
        var levelWidth = (int)tileSet.TileSetLevels[level].Width;
        var levelHeight = (int)tileSet.TileSetLevels[level].Height;
        if (maxX > levelWidth - 1) maxX = levelWidth - 1;
        if (maxY > levelHeight - 1) maxY = levelHeight - 1;
    }

    /// <summary>
    ///     Checks if a region exists in a given layer.
    /// </summary>
    /// <param name="level">The index of the mip level the region lives at.</param>
    /// <param name="layer">The index of the layer the region must exist in.</param>
    /// <param name="minX">The first tile column of the region, inclusive.</param>
    /// <param name="minY">The first tile row of the region, inclusive.</param>
    /// <param name="maxX">The last tile column of the region, inclusive.</param>
    /// <param name="maxY">The last tile row of the region, inclusive.</param>
    /// <returns>
    ///     <see langword="true" /> when every tile of the region exists in that layer; otherwise
    ///     <see langword="false" />, meaning the layer leaves part of the region empty.
    /// </returns>
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