using BG3PakViewer.Shared.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Controls.ViewModels;

public partial class ImagePreviewViewModel : DisposableViewModel
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ImagePreviewViewModel" /> class.
    /// </summary>
    /// <param name="preview">The decoded preview image owned by this view model.</param>
    public ImagePreviewViewModel(Image preview)
    {
        Preview = preview;
    }

    /// <summary>
    ///     The decoded preview image, platform-agnostic (no WPF types). The view converts it for
    ///     display; this view model owns it and releases it when disposed.
    /// </summary>
    [ObservableProperty]
    public partial Image? Preview { get; private set; }

    /// <summary>
    ///     Detaches the image from the view before it is released.
    /// </summary>
    /// <param name="disposing">Whether managed resources are released.</param>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        // Detach the view from the image before releasing it.
        var image = Preview;
        Preview = null;
        image?.Dispose();
    }
}
