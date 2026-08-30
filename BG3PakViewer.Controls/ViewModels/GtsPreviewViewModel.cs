using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using BG3PakViewer.Extensions;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.VirtualTextures;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     GTS virtual texture preview: lists textures on the left; selecting one extracts and decodes
///     the selected layer to a bitmap in the background, with cancellation and progress reporting.
/// </summary>
public partial class GtsPreviewViewModel : DisposableViewModel
{
    private readonly VirtualTileSetExtractor _extractor;
    private CancellationTokenSource? _cts;

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
        if (_cts is not null)
            await _cts.CancelAsync();
        var cts = new CancellationTokenSource();
        _cts = cts;

        if (SelectedTexture is null) return;
        var meta = SelectedTexture.Meta;
        var layer = SelectedLayerIndex;

        try
        {
            IsBusy = true;
            StatusText = I18NExtension.Translate(LangKeys.GtsExtracting, "Extracting…");
            Progress = 0;

            var progress = new Progress<(int Done, int Total)>(p =>
                Progress = p.Total == 0 ? 0 : p.Done * 100.0 / p.Total);

            using var ddsStream = new MemoryStream();
            var extracted = await Task.Run(
                () => _extractor.ExtractTexture(layer, meta, ddsStream, progress, cts.Token),
                cts.Token);

            if (cts.IsCancellationRequested) return;
            if (!extracted)
            {
                Preview = null;
                StatusText = I18NExtension.Translate(LangKeys.GtsNoDataForLayer, "No data for this layer");
                return;
            }

            ddsStream.Position = 0;
            using var image = await ImageLoader.LoadAsync(ddsStream, ".dds");
            if (cts.IsCancellationRequested) return;

            Preview = image?.ToBitmapSource();
            StatusText = image is null
                ? I18NExtension.Translate(LangKeys.GtsDecodeFailed, "Decode failed")
                : $"{meta.Width} × {meta.Height}";
        }
        catch (OperationCanceledException)
        {
            // A new selection canceled the previous task; ignore
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to preview GTS texture: {Name}", meta.Name);
            StatusText = I18NExtension.Translate(LangKeys.GtsPreviewFailed, "Preview failed");
        }
        finally
        {
            if (!cts.IsCancellationRequested) IsBusy = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        base.Dispose(disposing);
        _ = _cts?.CancelAsync();
        _extractor.Dispose();
    }
}