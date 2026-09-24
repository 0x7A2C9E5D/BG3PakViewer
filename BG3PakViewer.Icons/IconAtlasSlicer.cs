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
    /// <param name="grid">The cell size detected from the atlas content, or <c>null</c> when the sheet shows no grid.</param>
    /// <returns>The icons. The caller owns and disposes them.</returns>
    /// <remarks>
    ///     A sheet without a grid holds a single icon instead of a row of them, so it is cut down to the
    ///     pixels that icon is drawn on, which is what keeps its padding out of the preview.
    /// </remarks>
    public static IReadOnlyList<Image> Slice(Image atlas, IconAtlasGrid? grid)
    {
        // Atlases reach here as Bgra32, Bgr24 or gray; normalizing once keeps both the scan and the cuts
        // on a single layout, and a plain Bgra32 atlas is used as it is.
        var source = atlas as Image<Bgra32>;
        var ownsSource = source is null;
        source ??= atlas.CloneAs<Bgra32>();

        try
        {
            return grid is not { } detected
                ? CropSingleIconToContentBounds(source)
                : Slice(source, Math.Max(1, detected.CellWidth), Math.Max(1, detected.CellHeight));
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
                var cell = new Rectangle(x, y, cellWidth, cellHeight);

                // Every cell is judged on its own pixels alone. A coarser map would merge the padding of
                // two neighboring cells into one entry, and a pitch that is not a multiple of its
                // granularity then leaves the padding of a whole cell looking like an icon.
                if (FindContentBoundingBox(atlas, cell) is null) continue;

                icons.Add(atlas.Clone(context => context.Crop(cell)));
            }
        }

        return icons;
    }

    /// <summary>
    ///     Cuts a sheet that shows no grid down to the single icon it holds.
    /// </summary>
    /// <param name="atlas">The atlas to cut.</param>
    /// <returns>The icon, or an empty list when the sheet holds nothing visible.</returns>
    private static List<Image> CropSingleIconToContentBounds(Image<Bgra32> atlas)
    {
        var whole = new Rectangle(0, 0, atlas.Width, atlas.Height);
        return FindContentBoundingBox(atlas, whole) is { } icon ? [atlas.Clone(context => context.Crop(icon))] : [];
    }

    /// <summary>
    ///     Finds the pixels a region of the atlas is drawn on.
    /// </summary>
    /// <param name="atlas">The atlas to look at.</param>
    /// <param name="area">The region to scan.</param>
    /// <returns>The bounds of the visible pixels, or <c>null</c> when the region holds nothing but padding.</returns>
    private static Rectangle? FindContentBoundingBox(Image<Bgra32> atlas, Rectangle area)
    {
        var left = int.MaxValue;
        var top = int.MaxValue;
        var right = -1;
        var bottom = -1;

        for (var row = area.Top; row < area.Bottom; row += SampleStride)
            for (var column = area.Left; column < area.Right; column += SampleStride)
            {
                if (!IsVisibleContentPixel(atlas[column, row])) continue;

                left = Math.Min(left, column);
                top = Math.Min(top, row);
                right = Math.Max(right, column);
                bottom = Math.Max(bottom, row);
            }

        return right < 0 ? null : new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>
    ///     Tells whether a pixel of the atlas belongs to an icon rather than to the padding.
    /// </summary>
    /// <param name="pixel">The pixel to look at.</param>
    /// <returns><c>true</c> when the pixel is visible.</returns>
    private static bool IsVisibleContentPixel(Bgra32 pixel)
    {
        if (pixel.A <= VisibleThreshold) return false;

        return pixel is not { B: <= VisibleThreshold, G: <= VisibleThreshold, R: <= VisibleThreshold };
    }
}