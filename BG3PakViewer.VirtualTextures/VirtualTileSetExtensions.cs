using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

/// <summary>
///     Effective tile dimensions of a <see cref="VirtualTileSet" />: the tile width/height after the
///     shared border used for block compression is trimmed. The rule lives here only, so consumers
///     cannot drift apart.
/// </summary>
public static class VirtualTileSetExtensions
{
    extension(VirtualTileSet tileSet)
    {
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
