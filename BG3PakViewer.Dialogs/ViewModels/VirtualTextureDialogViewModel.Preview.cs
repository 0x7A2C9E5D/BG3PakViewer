using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     Extracting and decoding the selected layer of the selected texture, including the state it
///     publishes and the cancellation of the load that a new selection or layer replaces.
/// </summary>
public partial class VirtualTextureDialogViewModel
{
    private CancellationTokenSource? _previewCancellation;

    /// <summary>
    ///     The decoded preview of the selected texture, platform-agnostic; the view converts it.
    /// </summary>
    [ObservableProperty]
    public partial Image? Preview { get; private set; }

    /// <summary>
    ///     The resource key, or the untranslated image dimensions, the preview status line shows.
    /// </summary>
    [ObservableProperty]
    public partial string PreviewStatusKey { get; private set; } = string.Empty;

    /// <summary>
    ///     Whether the preview of the selected texture is being loaded.
    /// </summary>
    [ObservableProperty]
    public partial bool IsPreviewing { get; private set; }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnIsPreviewingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBusy));
    }

    /// <summary>
    ///     Loads the preview when the selection changes.
    /// </summary>
    /// <param name="value"></param>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedRowChanged(VirtualTextureItemViewModel? value)
    {
        _ = LoadPreviewAsync();
    }

    /// <summary>
    ///     Loads the preview when the selected layer changes.
    /// </summary>
    /// <param name="value"></param>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedLayerIndexChanged(int value)
    {
        _ = LoadPreviewAsync();
    }

    /// <summary>
    ///     Loads the preview of the selected layer of the selected texture.
    /// </summary>
    private async Task LoadPreviewAsync()
    {
        var row = SelectedRow;
        if (row is null)
        {
            await ClearPreviewAsync();
            return;
        }

        var layer = SelectedLayerIndex;
        var cts = await BeginPreviewAsync();

        try
        {
            await using var stream = await _extractor.ExtractLayerAsync(row.Entry, layer,
                new Progress<double>(value =>
                {
                    // A replaced load can still report progress after cancellation; ignore it so the
                    // current preview keeps its own value.
                    if (!cts.IsCancellationRequested) PreviewProgress = value;
                }), cts.Token);
            if (cts.IsCancellationRequested) return;

            if (stream is null)
            {
                Preview = null;
                PreviewStatusKey = LangKeys.TitleNoData;
                return;
            }

            // Ownership of the image is transferred to Preview; it is disposed when replaced or disposed.
            var image = await Task.Run(() => ImageLoader.LoadAsync(stream, ".dds"), cts.Token);
            if (cts.IsCancellationRequested)
            {
                image?.Dispose();
                return;
            }

            Preview = image;
            PreviewStatusKey = image is null
                ? LangKeys.TitleDecodeFailed
                : $"{image.Width} × {image.Height}";
        }
        catch (OperationCanceledException)
        {
            // A new selection or layer replaced this load; ignore.
        }
        catch (Exception ex)
        {
            Preview = null;
            PreviewStatusKey = LangKeys.TitlePreviewFailed;
            Log.Error(ex, "Failed to preview virtual texture: {Name}", row.Name);
        }
        finally
        {
            if (!cts.IsCancellationRequested) IsPreviewing = false;
        }
    }

    /// <summary>
    ///     Cancels the running load, if any, and starts a new one.
    /// </summary>
    /// <returns>The cancellation source of the new load.</returns>
    private async Task<CancellationTokenSource> BeginPreviewAsync()
    {
        if (_previewCancellation is not null)
            await _previewCancellation.CancelAsync();
        var cts = new CancellationTokenSource();
        _previewCancellation = cts;
        IsPreviewing = true;
        PreviewStatusKey = LangKeys.TitleExtracting;
        PreviewProgress = 0;
        return cts;
    }

    /// <summary>
    ///     Cancels the running load, if any, and clears the preview.
    /// </summary>
    private async Task ClearPreviewAsync()
    {
        if (_previewCancellation is not null)
            await _previewCancellation.CancelAsync();
        _previewCancellation = null;
        Preview = null;
        PreviewStatusKey = string.Empty;
        IsPreviewing = false;
        PreviewProgress = 0;
    }

    /// <summary>
    ///     Disposes the old preview when the new one is set.
    /// </summary>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    partial void OnPreviewChanging(Image? oldValue, Image? newValue)
    {
        if (!ReferenceEquals(oldValue, newValue)) oldValue?.Dispose();
    }
}
