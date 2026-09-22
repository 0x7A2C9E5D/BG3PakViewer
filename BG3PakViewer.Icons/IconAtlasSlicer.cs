using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Icons;

/// <summary>
///     Cuts the icons of an icon atlas out of the sheet.
/// </summary>
/// <remarks>
///     The cells of the detected <see cref="IconAtlasGrid" /> are cut into one image each, so that a
///     preview only has to display the icons and never has to reason about where they sit in the sheet.
///     Cells that hold nothing but the padding of the sheet are left out.
/// </remarks>
public static class IconAtlasSlicer
{
    /// <summary>
    ///     Only every second pixel of a cell is looked at; padding is far too big to be missed by that.
    /// </summary>
    private const int SampleStride = 2;

    /// <summary>
    ///     A pixel this transparent, and this dark, does not count as content.
    /// </summary>
    private const int VisibleThreshold = 16;

    /// <summary>
    ///     Slices an atlas into one image per icon, in the order of the atlas.
    /// </summary>
    /// <param name="atlas">The decoded atlas. It is not disposed by this method.</param>
    /// <param name="grid">The cell size detected from the atlas content.</param>
    /// <returns>The icons. The caller owns and disposes them.</returns>
    public static IReadOnlyList<Image> Slice(Image atlas, IconAtlasGrid grid)
    {
        var cellWidth = Math.Max(1, grid.CellWidth);
        var cellHeight = Math.Max(1, grid.CellHeight);

        // Atlases reach here as Bgra32, Bgr24 or gray; normalizing once keeps both the scan and the cuts
        // on a single layout, and a plain Bgra32 atlas is used as it is.
        var source = atlas as Image<Bgra32>;
        var ownsSource = source is null;
        source ??= atlas.CloneAs<Bgra32>();

        try
        {
            return Slice(source, cellWidth, cellHeight);
        }
        finally
        {
            if (ownsSource) source.Dispose();
        }
    }

    private static List<Image> Slice(Image<Bgra32> atlas, int cellWidth, int cellHeight)
    {
        // The detected pitch rarely divides the atlas exactly - a portrait sheet has a half used column
        // and row at its edge - so the remainder is dropped instead of being stretched into the last cell.
        var columns = Math.Max(1, atlas.Width / cellWidth);
        var rows = Math.Max(1, atlas.Height / cellHeight);
        var icons = new List<Image>();

        for (var row = 0; row < rows; row++)
        {
            var y = row * cellHeight;
            for (var column = 0; column < columns; column++)
            {
                var x = column * cellWidth;

                // Every cell is judged on its own pixels alone. A coarser map would merge the padding of
                // two neighboring cells into one entry, and a pitch that is not a multiple of its
                // granularity then leaves the padding of a whole cell looking like an icon.
                if (!HasContent(atlas, x, y, cellWidth, cellHeight)) continue;

                var cell = new Rectangle(x, y, cellWidth, cellHeight);
                icons.Add(atlas.Clone(context => context.Crop(cell)));
            }
        }

        return icons;
    }

    /// <summary>
    ///     Tells whether a cell of the atlas holds anything but padding.
    /// </summary>
    /// <param name="atlas">The atlas to look at.</param>
    /// <param name="x">The left edge of the cell.</param>
    /// <param name="y">The top edge of the cell.</param>
    /// <param name="width">The width of the cell.</param>
    /// <param name="height">The height of the cell.</param>
    /// <returns><c>true</c> when the cell holds at least one visible pixel.</returns>
    private static bool HasContent(Image<Bgra32> atlas, int x, int y, int width, int height)
    {
        for (var row = y; row < y + height; row += SampleStride)
        for (var column = x; column < x + width; column += SampleStride)
        {
            var pixel = atlas[column, row];
            if (pixel.A <= VisibleThreshold) continue;
            if (pixel is { B: <= VisibleThreshold, G: <= VisibleThreshold, R: <= VisibleThreshold }) continue;

            return true;
        }

        return false;
    }
}