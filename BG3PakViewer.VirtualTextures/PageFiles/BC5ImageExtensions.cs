using System.Drawing;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.PageFiles;

/// <summary>
///     Copies the tiles of a band into the strip buffer they are stitched into.
/// </summary>
internal static class BC5ImageExtensions
{
    extension(BC5Image tile)
    {
        /// <summary>
        ///     Copies this tile into <paramref name="strip" /> at band column <paramref name="column" />,
        ///     skipping the border every tile carries on each side.
        /// </summary>
        /// <param name="strip">The band strip the tile is copied into.</param>
        /// <param name="content">The pixel size of one tile, without its border.</param>
        /// <param name="border">The border, in pixels, that every tile carries on each side.</param>
        /// <param name="column">The band column the tile is copied to.</param>
        public void CopyTileTo(BC5Image strip, Size content, int border, int column)
        {
            tile.CopyTo(strip, border, border, column * content.Width, 0, content.Width, content.Height);
        }
    }
}
