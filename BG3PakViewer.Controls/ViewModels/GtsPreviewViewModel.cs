using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;
using BG3PakViewer.Extensions;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Messaging;
using BG3PakViewer.Shared.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
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
    private readonly VirtualTextureLoader _extractor;
    private CancellationTokenSource? _cts;

    public GtsPreviewViewModel(VirtualTextureLoader extractor)
    {
        _extractor = extractor;
        Layers =
        [
            .. Enumerable.Range(0, extractor.LayerCount)
                .Select(i => $"Layer {i}")
        ];
        foreach (var meta in extractor.GetTextures()) Textures.Add(new GtsTextureItemViewModel(meta));
        TexturesView = CollectionViewSource.GetDefaultView(Textures);
        TexturesView.Filter = FilterTexture;
        SelectedTexture = Textures.FirstOrDefault();
        WeakReferenceMessenger.Default.Register<SearchMessage>(this, (_, message) => OnSearchMessage(message));
    }

    private ObservableCollection<GtsTextureItemViewModel> Textures { get; } = [];

    /// <summary>Filtered view of <see cref="Textures" />, driven by <see cref="SearchText" />.</summary>
    public ICollectionView TexturesView { get; }

    public IReadOnlyList<string> Layers { get; }

    [ObservableProperty] public partial GtsTextureItemViewModel? SelectedTexture { get; set; }

    [ObservableProperty] private partial string? SearchText { get; set; }

    [ObservableProperty] public partial int SelectedLayerIndex { get; set; }

    [ObservableProperty] public partial ImageSource? Preview { get; private set; }

    [ObservableProperty] public partial bool IsBusy { get; set; }

    [ObservableProperty] public partial string? StatusText { get; private set; }

    [ObservableProperty] public partial double Progress { get; set; }

    private void OnSearchMessage(SearchMessage message)
    {
        SearchText = string.IsNullOrEmpty(message.Text) ? null : message.Text;
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSearchTextChanged(string? value)
    {
        TexturesView.Refresh();
    }

    private bool FilterTexture(object item)
    {
        return string.IsNullOrWhiteSpace(SearchText) ||
               ((GtsTextureItemViewModel)item).DisplayName
               .Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

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
            await LoadPreviewCoreAsync(meta, layer, cts);
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

    private async Task LoadPreviewCoreAsync(FourCCTextureMeta meta, int layer, CancellationTokenSource cts)
    {
        await using var ddsStream = await _extractor.ExtractAsync(meta, layer,
            new Progress<double>(p => Progress = p), cts.Token);
        if (cts.IsCancellationRequested) return;

        if (ddsStream is null)
        {
            ShowNoData();
            return;
        }

        using var image = await ImageLoader.DecodeDdsAsync(ddsStream);
        if (cts.IsCancellationRequested) return;

        ShowPreview(image);
    }

    private void ShowNoData()
    {
        Preview = null;
        StatusText = Strings.GtsNoDataForLayer;
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

    private void ShowPreview(Image? image)
    {
        Preview = image?.ToBitmapSource();
        StatusText = image is null
            ? Strings.GtsDecodeFailed
            : $"{image.Width} × {image.Height}";
    }

    protected override void Dispose(bool disposing)
    {
        WeakReferenceMessenger.Default.Unregister<SearchMessage>(this);
        base.Dispose(disposing);
        if (!disposing) return;
        _ = _cts?.CancelAsync();
        _extractor.Dispose();
    }
}