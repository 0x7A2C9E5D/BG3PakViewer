using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using LSLib.VirtualTextures;
using Serilog;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     Extracting and decoding the selected layer of the selected texture, including the
///     cancellation of the load that a new selection replaces.
/// </summary>
public partial class VirtualTexturePreviewViewModel
{
    /// <summary>
    ///     Loads the preview when the selected texture or layer changes.
    /// </summary>
    /// <param name="value"></param>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedTextureChanged(VirtualTextureItemViewModel? value)
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
    ///     Loads the preview.
    /// </summary>
    private async Task LoadPreviewAsync()
    {
        var texture = SelectedTexture;
        var layer = SelectedLayerIndex;

        // No selection (for example the filter matched no texture) is not a load: cancel the
        // running extraction and clear the preview, otherwise the status line stays on the
        // "extracting" resource key forever.
        if (texture is null)
        {
            await ClearPreviewAsync();
            return;
        }

        var cts = await BeginLoadAsync();
        var meta = texture.Meta;

        try
        {
            await LoadPreviewCoreAsync(meta, layer, cts);
        }
        catch (OperationCanceledException)
        {
            // A new selection canceled the previous task; ignore
        }
        catch (Exception ex)
        {
            Preview = null;
            StatusKey = LangKeys.TitlePreviewFailed;
            Log.Error(ex, "Failed to preview virtual texture: {Name}", meta.Name);
        }
        finally
        {
            if (!cts.IsCancellationRequested) IsBusy = false;
        }
    }

    /// <summary>
    ///     Loads the preview core.
    /// </summary>
    /// <param name="meta"></param>
    /// <param name="layer"></param>
    /// <param name="cts"></param>
    private async Task LoadPreviewCoreAsync(FourCCTextureMeta meta, int layer, CancellationTokenSource cts)
    {
        await using var ddsStream = await _loader.ExtractAsync(meta, layer,
            new Progress<double>(p => Progress = p), cts.Token);
        if (cts.IsCancellationRequested) return;

        if (ddsStream is null)
        {
            ShowNoData();
            return;
        }

        // Ownership of the image is transferred to Preview; it is disposed when replaced or disposed.
        // Decoding is CPU bound, so keep it off the UI thread the view model now lives on.
        var image = await Task.Run(() => ImageLoader.LoadAsync(ddsStream, ".dds"), cts.Token);
        if (cts.IsCancellationRequested)
        {
            image?.Dispose();
            return;
        }

        ShowPreview(image);
    }

    /// <summary>
    ///     Clears the preview and points the status line at the no-data resource key.
    /// </summary>
    private void ShowNoData()
    {
        Preview = null;
        StatusKey = LangKeys.TitleNoData;
    }

    /// <summary>
    ///     Cancels the running load, if any, and clears the preview and the status line.
    /// </summary>
    private async Task ClearPreviewAsync()
    {
        if (_cts is not null)
            await _cts.CancelAsync();
        _cts = null;
        Preview = null;
        IsBusy = false;
        Progress = 0;
        StatusKey = string.Empty;
    }

    /// <summary>
    ///     Begins a load operation.
    /// </summary>
    /// <returns></returns>
    private async Task<CancellationTokenSource> BeginLoadAsync()
    {
        if (_cts is not null)
            await _cts.CancelAsync();
        var cts = new CancellationTokenSource();
        _cts = cts;

        IsBusy = true;
        StatusKey = LangKeys.TitleExtracting;
        Progress = 0;
        return cts;
    }

    /// <summary>
    ///     Shows the preview.
    /// </summary>
    /// <param name="image"></param>
    private void ShowPreview(Image? image)
    {
        Preview = image;
        StatusKey = image is null
            ? LangKeys.TitleDecodeFailed
            : $"{image.Width} × {image.Height}";
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