using BG3PakViewer.Dialogs.ViewModels.States;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.VirtualTextures.Extraction;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
    public bool IsPreviewing => _state.IsPreviewing;

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
        var (cts, run) = await BeginPreviewAsync();
        var phase = VirtualTexturePreviewState.Empty;

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
                phase = VirtualTexturePreviewState.Empty;
                return;
            }

            // Ownership of the image is transferred to Preview; it is disposed when replaced or disposed.
            var image = await Task.Run(async () =>
            {
                var decoded = await ImageLoader.LoadAsync(stream, ".dds");
                if (decoded is not null) await ApplyGamePreviewSemanticsAsync(decoded, row, layer, cts.Token);
                return decoded;
            }, cts.Token);
            if (cts.IsCancellationRequested)
            {
                image?.Dispose();
                return;
            }

            Preview = image;
            phase = image is null
                ? VirtualTexturePreviewState.Failed
                : VirtualTexturePreviewState.Loaded;
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
            phase = VirtualTexturePreviewState.Failed;
            Log.Error(ex, "Failed to preview virtual texture: {Name}", row.Name);
        }
        finally
        {
            _state.EndPreview(run, phase);
        }
    }

    /// <summary>
    ///     Cancels the running load, if any, and starts a new one.
    /// </summary>
    /// <returns>The cancellation source and the generation of the new load.</returns>
    private async Task<(CancellationTokenSource Cts, int Run)> BeginPreviewAsync()
    {
        if (_previewCancellation is not null)
            await _previewCancellation.CancelAsync();
        var cts = new CancellationTokenSource();
        _previewCancellation = cts;
        var run = _state.BeginPreview();
        PreviewStatusKey = LangKeys.TitleExtracting;
        PreviewProgress = 0;
        return (cts, run);
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
        _state.ClearPreview();
        PreviewProgress = 0;
    }

    /// <summary>
    ///     Aligns the decoded preview with the way the game reads the layer, so it looks like the texture
    ///     does in the game. The exported DDS is written straight from the extractor and keeps the exact
    ///     bytes the upstream LSLib convention produces; only the preview image is touched.
    /// </summary>
    /// <remarks>
    ///     The tiles are standard BC3 blocks, but how their channels are read depends on the data type
    ///     the tile set declares for the layer. Color layers keep their alpha: where the source texture
    ///     has real transparency the game blends it, so the preview shows it too, while a fully
    ///     transparent color layer carries no transparency at all (the game renders it opaques). Data
    ///     layers (normals, packed material parameters) use their fourth channel as a shader input
    ///     rather than as transparency, so they are always shown opaque.
    /// </remarks>
    /// <param name="image">The decoded preview image, adjusted in place.</param>
    /// <param name="row">The row the preview shows.</param>
    /// <param name="layer">The layer the preview shows.</param>
    /// <param name="cancellationToken">Cancels the adjustment.</param>
    private async Task ApplyGamePreviewSemanticsAsync(Image image, VirtualTextureItemViewModel row, int layer,
        CancellationToken cancellationToken)
    {
        VirtualTextureLayerKind kind;
        try
        {
            kind = await _extractor.GetLayerKindAsync(row.Entry, layer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An unreadable tile set must not break the preview; the image is shown as decoded instead.
            Log.Warning(ex, "Failed to read the preview semantics of {TileSet}", row.Source);
            return;
        }

        if (kind == VirtualTextureLayerKind.Data)
            MakeOpaques(image);
        else
            MakeVisibleIfFullyTransparent(image);
    }

    /// <summary>
    ///     Makes the image opaque when every pixel is fully transparent, keeping the RGB bytes. Images
    ///     with any real alpha are left untouched, and formats without an alpha channel need nothing.
    /// </summary>
    /// <param name="image">The image to adjust in place.</param>
    private static void MakeVisibleIfFullyTransparent(Image image)
    {
        switch (image)
        {
            case Image<Bgra32> bgra32:
                MakeVisibleIfFullyTransparent(bgra32);
                break;
            case Image<Bgra4444> bgra4444:
                MakeVisibleIfFullyTransparent(bgra4444);
                break;
            case Image<Bgra5551> bgra5551:
                MakeVisibleIfFullyTransparent(bgra5551);
                break;
        }
    }

    /// <summary>
    ///     Sets the alpha of every pixel to opaque, but only when no pixel of the image has any alpha.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format of the image.</typeparam>
    /// <param name="image">The image to adjust in place.</param>
    private static void MakeVisibleIfFullyTransparent<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var hasVisibleAlpha = false;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height && !hasVisibleAlpha; y++)
            {
                var rowSpan = accessor.GetRowSpan(y);
                foreach (var pixel in rowSpan)
                    if (pixel.ToRgba32().A != 0)
                    {
                        hasVisibleAlpha = true;
                        break;
                    }
            }
        });
        if (hasVisibleAlpha) return;

        MakeOpaques(image);
    }

    /// <summary>
    ///     Makes the image opaque, keeping the RGB bytes. Formats without an alpha channel are left
    ///     unchanged.
    /// </summary>
    /// <param name="image">The image to adjust in place.</param>
    private static void MakeOpaques(Image image)
    {
        switch (image)
        {
            case Image<Bgra32> bgra32:
                MakeOpaques(bgra32);
                break;
            case Image<Bgra4444> bgra4444:
                MakeOpaques(bgra4444);
                break;
            case Image<Bgra5551> bgra5551:
                MakeOpaques(bgra5551);
                break;
        }
    }

    /// <summary>
    ///     Sets the alpha of every pixel to opaque.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format of the image.</typeparam>
    /// <param name="image">The image to adjust in place.</param>
    private static void MakeOpaques<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var rowSpan = accessor.GetRowSpan(y);
                for (var x = 0; x < rowSpan.Length; x++)
                {
                    var rgba = rowSpan[x].ToRgba32();
                    rgba.A = byte.MaxValue;
                    rowSpan[x] = TPixel.FromRgba32(rgba);
                }
            }
        });
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
