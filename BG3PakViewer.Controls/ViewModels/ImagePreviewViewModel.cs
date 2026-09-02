using BG3PakViewer.Shared.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Controls.ViewModels;

public partial class ImagePreviewViewModel : DisposableViewModel
{
    /// <summary>
    ///     The decoded image, platform-agnostic (no WPF types). The view converts it for display;
    ///     this view model owns it and disposes it when replaced or disposed.
    /// </summary>
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    [ObservableProperty] public partial Image? Data { get; set; }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnDataChanged(Image? value)
    {
        Log.Information("ImageFileViewModel.DataChanged");
    }

    partial void OnDataChanging(Image? oldValue, Image? newValue)
    {
        if (!ReferenceEquals(oldValue, newValue)) oldValue?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        // Detach the view from the image before releasing it.
        Data = null;
    }
}
