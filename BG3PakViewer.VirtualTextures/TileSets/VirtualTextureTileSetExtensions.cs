using System.Drawing;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.TileSets;

/// <summary>
///     Sizes of a <see cref="VirtualTileSet" />: the page size of its page files, the full tile size the
///     tile set declares and the effective size left after the shared border used for block compression
///     is trimmed. The rules live here only, so consumers cannot drift apart.
/// </summary>
internal static class VirtualTextureTileSetExtensions
{
    extension(VirtualTileSet tileSet)
    {
        /// <summary>
        ///     The aligned size of one page in the page files, in bytes.
        /// </summary>
        public int PageSize => (int)tileSet.Header.PageSize;

        /// <summary>
        ///     The full pixel size of one tile, including the border every tile carries on each side.
        /// </summary>
        public Size TileSize => new(tileSet.Header.TileWidth, tileSet.Header.TileHeight);

        /// <summary>
        ///     The border, in pixels, that every tile carries on each side.
        /// </summary>
        public int TileBorder => tileSet.Header.TileBorder;

        /// <summary>
        ///     Effective tile width after trimming the shared border.
        /// </summary>
        public int EffectiveTileWidth => tileSet.Header.TileWidth - tileSet.Header.TileBorder * 2;

        /// <summary>
        ///     Effective tile height after trimming the shared border.
        /// </summary>
        public int EffectiveTileHeight => tileSet.Header.TileHeight - tileSet.Header.TileBorder * 2;
    }
}