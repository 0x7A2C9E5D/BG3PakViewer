using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using Serilog;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     Extracting and decoding the selected layer of the selected texture, including the cancellation
///     of the load that a new selection or layer replaces.
/// </summary>
public partial class VirtualTextureToolboxDialogViewModel
{
    private CancellationTokenSource? _previewCancellation;

    /// <summary>
    ///     Loads the preview when the selection changes.
    /// </summary>
    /// <param name="value"></param>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedRowChanged(VirtualTextureToolboxTextureViewModel? value)
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
                new Progress<double>(value => Progress = value), cts.Token);
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
            Log.Error(ex, "Failed to preview virtual texture: {name}", row.DisplayName);
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
        Progress = 0;
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
        Progress = 0;
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
