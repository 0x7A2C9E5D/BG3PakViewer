using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using BG3PakViewer.Extensions;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.ViewModels;
using BG3PakViewer.VirtualTextures;
using CommunityToolkit.Mvvm.ComponentModel;
using LSLib.VirtualTextures;
using Serilog;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     GTS virtual texture preview: lists textures on the left; selecting one extracts and decodes
///     the selected layer to a bitmap in the background, with cancellation and progress reporting.
/// </summary>
public partial class GtsPreviewViewModel : DisposableViewModel
{
    private readonly VirtualTileSetExtractor _extractor;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public GtsPreviewViewModel(VirtualTileSetExtractor extractor)
    {
        _extractor = extractor;
        Layers =
        [
            .. Enumerable.Range(0, extractor.LayerCount)
                .Select(i => $"Layer {i}")
        ];
        foreach (var meta in extractor.GetTextures()) Textures.Add(new GtsTextureItemViewModel(meta));
        SelectedTexture = Textures.FirstOrDefault();
    }

    public ObservableCollection<GtsTextureItemViewModel> Textures { get; } = [];

    public IReadOnlyList<string> Layers { get; }

    [ObservableProperty] public partial GtsTextureItemViewModel? SelectedTexture { get; set; }

    [ObservableProperty] public partial int SelectedLayerIndex { get; set; }

    [ObservableProperty] public partial ImageSource? Preview { get; private set; }

    [ObservableProperty] public partial bool IsBusy { get; set; }

    [ObservableProperty] public partial string? StatusText { get; private set; }

    [ObservableProperty] public partial double Progress { get; set; }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedTextureChanged(GtsTextureItemViewModel? value)
    {
        _ = LoadPreviewAsync();
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedLayerIndexChanged(int value)
    {
        _ = LoadPreviewAsync();
    }

    private async Task LoadPreviewAsync()
    {
        var cts = await BeginLoadAsync();
        if (SelectedTexture is null) return;
        var meta = SelectedTexture.Meta;
        var layer = SelectedLayerIndex;

        try
        {
            using var ddsStream = await ExtractDdsAsync(meta, layer, cts);
            if (cts.IsCancellationRequested) return;
            if (ddsStream is null)
            {
                Preview = null;
                StatusText = Strings.GtsNoDataForLayer;
                return;
            }

            using var image = await DecodeDdsAsync(ddsStream);
            if (cts.IsCancellationRequested) return;

            ShowPreview(image);
        }
        catch (OperationCanceledException)
        {
            // A new selection canceled the previous task; ignore
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to preview GTS texture: {Name}", meta.Name);
            StatusText = Strings.GtsPreviewFailed;
        }
        finally
        {
            if (!cts.IsCancellationRequested) IsBusy = false;
        }
    }

    private async Task<CancellationTokenSource> BeginLoadAsync()
    {
        if (_cts is not null)
            await _cts.CancelAsync();
        var cts = new CancellationTokenSource();
        _cts = cts;

        IsBusy = true;
        StatusText = Strings.GtsExtracting;
        Progress = 0;
        return cts;
    }

    private async Task<MemoryStream?> ExtractDdsAsync(FourCCTextureMeta meta, int layer, CancellationTokenSource cts)
    {
        var progress = new Progress<(int Done, int Total)>(p =>
            Progress = p.Total == 0 ? 0 : p.Done * 100.0 / p.Total);

        var ddsStream = new MemoryStream();
        try
        {
            var extracted = await Task.Run(
                () => _extractor.ExtractTexture(layer, meta, ddsStream, progress, cts.Token),
                cts.Token);
            if (!extracted)
            {
                await ddsStream.DisposeAsync();
                return null;
            }

            ddsStream.Position = 0;
            return ddsStream;
        }
        catch
        {
            await ddsStream.DisposeAsync();
            throw;
        }
    }

    private static async Task<Image?> DecodeDdsAsync(MemoryStream ddsStream)
    {
        return await ImageLoader.LoadAsync(ddsStream, ".dds");
    }

    private void ShowPreview(Image? image)
    {
        Preview = image?.ToBitmapSource();
        StatusText = image is null
            ? Strings.GtsDecodeFailed
            : $"{image.Width} × {image.Height}";
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose(disposing);
        if (!disposing) return;
        _ = _cts?.CancelAsync();
        _extractor.Dispose();
    }
}