using System.Numerics;
using System.Windows.Media.Imaging;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     A single icon of an atlas, already sliced by the loader, together with the place it takes in the
///     sheet.
/// </summary>
/// <remarks>
///     The atlas does not name its icons, so the UV range of the cell the icon was cut from is the only
///     identity it has; it is what lets a user find the icon again in the sheet or match it against
///     external data.
/// </remarks>
public sealed class IconAtlasCellViewModel(BitmapSource image, Vector4 uv)
{
    /// <summary>
    ///     The sliced bitmap of this icon.
    /// </summary>
    public BitmapSource Image { get; } = image;

    /// <summary>
    ///     The UV range of the icon in the atlas, as (min u, min v, max u, max v). The UVs are normalized
    ///     against the size of the sheet, with the origin at its top left corner.
    /// </summary>
    public Vector4 UV { get; } = uv;

    /// <summary>
    ///     Width of the cell in the grid, in device independent pixels. The icon keeps the size it has in
    ///     the atlas instead of being stretched to a fixed one.
    /// </summary>
    public double Width { get; } = image.PixelWidth;

    /// <summary>
    ///     Height of the cell in the grid, in device independent pixels. The icon keeps the size it has in
    ///     the atlas instead of being stretched to a fixed one.
    /// </summary>
    public double Height { get; } = image.PixelHeight;
}
