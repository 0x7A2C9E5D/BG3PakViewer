using System.Windows.Media.Imaging;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     A single icon of an atlas, already sliced by the loader, together with the place it takes in the
///     sheet.
/// </summary>
/// <remarks>
///     The atlas does not name its icons, so the cell the icon was cut from is the only identity it has;
///     it is what lets a user find the icon again in the sheet or match it against external data.
/// </remarks>
public sealed class IconAtlasCellViewModel(BitmapSource image, int row, int column,
    int x, int y, int width, int height)
{
    /// <summary>
    ///     The sliced bitmap of this icon.
    /// </summary>
    public BitmapSource Image { get; } = image;

    /// <summary>
    ///     The place of the cell in the atlas grid, formatted for the tooltip of the cell. The row and the
    ///     column are one based, and a sheet without a grid has neither.
    /// </summary>
    public string PositionText => $"(L{row}, C{column})";

    /// <summary>
    ///     The offset of the icon in the atlas, formatted for the tooltip of the cell.
    /// </summary>
    public string CoordinatesText => $"({x}, {y}) px";

    /// <summary>
    ///     Width of the cell in the grid, in device independent pixels. The icon keeps the size it has in
    ///     the atlas instead of being stretched to a fixed one.
    /// </summary>
    public double Width { get; } = width;

    /// <summary>
    ///     Height of the cell in the grid, in device independent pixels. The icon keeps the size it has in
    ///     the atlas instead of being stretched to a fixed one.
    /// </summary>
    public double Height { get; } = height;

    /// <summary>
    ///     The size of the cell in the grid, formatted for the tooltip of the cell.
    /// </summary>
    public string SizeText => $"({Width:0} × {Height:0}) px";
}
