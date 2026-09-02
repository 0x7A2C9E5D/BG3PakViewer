using BG3PakViewer.Shared.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Controls.ViewModels;

public partial class ImagePreviewViewModel : DisposableViewModel
{
    /// <summary>
    ///     The decoded preview image, platform-agnostic (no WPF types). The view converts it for
    ///     display; this view model owns it and disposes it when replaced or disposed.
    /// </summary>
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    [ObservableProperty] public partial Image? Preview { get; set; }

    partial void OnPreviewChanged(Image? value)
    {
        // A new image arrives with every selection and carries no diagnostic value on its own,
        // so it is recorded at debug level only.
        Log.Debug("Image preview changed: {Width}x{Height}", value?.Width, value?.Height);
    }

    partial void OnPreviewChanging(Image? oldValue, Image? newValue)
    {
        if (!ReferenceEquals(oldValue, newValue)) oldValue?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        // Detach the view from the image before releasing it.
        Preview = null;
    }
}
