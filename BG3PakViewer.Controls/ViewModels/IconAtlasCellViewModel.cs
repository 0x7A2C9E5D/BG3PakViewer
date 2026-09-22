using System.Windows.Media.Imaging;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     A single icon of an atlas, already sliced by the loader.
/// </summary>
public sealed class IconAtlasCellViewModel(BitmapSource image)
{
    /// <summary>
    ///     The sliced bitmap of this icon.
    /// </summary>
    public BitmapSource Image { get; } = image;

    /// <summary>
    ///     Width of the cell in the grid, in device independent pixels. The icon keeps the size it has in
    ///     the atlas instead of being stretched to a fixed one.
    /// </summary>
    public double DisplayWidth => Image.PixelWidth;

    /// <summary>
    ///     Height of the cell in the grid, in device independent pixels. The icon keeps the size it has in
    ///     the atlas instead of being stretched to a fixed one.
    /// </summary>
    public double DisplayHeight => Image.PixelHeight;
}