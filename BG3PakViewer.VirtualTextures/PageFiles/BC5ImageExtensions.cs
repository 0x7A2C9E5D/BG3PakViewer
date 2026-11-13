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
        ///     trimming the border every tile carries on each side.
        /// </summary>
        /// <param name="strip">The band strip the tile is copied into.</param>
        /// <param name="size">The pixel size of one tile, without its border.</param>
        /// <param name="border">The border, in pixels, that every tile carries on each side.</param>
        /// <param name="column">The band column the tile is copied to.</param>
        public void CopyTo(BC5Image strip, Size size, int border, int column)
        {
            tile.CopyTo(strip, border, border, column * size.Width, 0, size.Width, size.Height);
        }
    }
}
