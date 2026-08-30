using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using BG3PakViewer.Extensions;
using BG3PakViewer.Loader;
using BG3PakViewer.VirtualTextures;
using CommunityToolkit.Mvvm.ComponentModel;
using LSLib.VirtualTextures;
using Serilog;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
/// GTS 虚拟纹理预览：左侧列出纹理，选中后在后台按图层提取并解码为位图。
/// 提取期间支持取消与进度报告。
/// </summary>
public partial class GtsPreviewViewModel : ObservableObject, IDisposable
{
    private readonly StreamingTileSetExtractor _extractor;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public ObservableCollection<GtsTextureItemViewModel> Textures { get; } = [];

    public IReadOnlyList<string> Layers { get; }

    [ObservableProperty] public partial GtsTextureItemViewModel? SelectedTexture { get; set; }

    [ObservableProperty] public partial int SelectedLayerIndex { get; set; }

    [ObservableProperty] public partial ImageSource? Preview { get; set; }

    [ObservableProperty] public partial bool IsBusy { get; set; }

    [ObservableProperty] public partial string? StatusText { get; set; }

    [ObservableProperty] public partial double Progress { get; set; }

    public GtsPreviewViewModel(StreamingTileSetExtractor extractor)
    {
        _extractor = extractor;
        Layers = Enumerable.Range(0, extractor.LayerCount)
            .Select(i => $"Layer {i}")
            .ToArray();
        foreach (var meta in extractor.GetTextures())
        {
            Textures.Add(new GtsTextureItemViewModel(meta));
        }
        SelectedTexture = Textures.FirstOrDefault();
    }

    partial void OnSelectedTextureChanged(GtsTextureItemViewModel? value) => _ = LoadPreviewAsync();

    partial void OnSelectedLayerIndexChanged(int value) => _ = LoadPreviewAsync();

    private async Task LoadPreviewAsync()
    {
        if(_cts is not null)
            await _cts.CancelAsync();
        var cts = new CancellationTokenSource();
        _cts = cts;

        if (SelectedTexture is null) return;
        var meta = SelectedTexture.Meta;
        var layer = SelectedLayerIndex;

        try
        {
            IsBusy = true;
            StatusText = "Extracting…";
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
                StatusText = "No data for this layer";
                return;
            }

            ddsStream.Position = 0;
            using var image = await ImageLoader.LoadAsync(ddsStream, ".dds");
            if (cts.IsCancellationRequested) return;

            Preview = image?.ToBitmapSource();
            StatusText = image is null ? "Decode failed" : $"{meta.Width} × {meta.Height}";
        }
        catch (OperationCanceledException)
        {
            // 切换选择时取消上一任务，忽略
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to preview GTS texture: {Name}", meta.Name);
            StatusText = "Preview failed";
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                IsBusy = false;
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (!disposing) return;
        _ = _cts?.CancelAsync();
        _extractor.Dispose();
    }
}
