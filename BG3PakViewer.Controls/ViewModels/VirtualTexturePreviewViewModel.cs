using System.Collections.ObjectModel;
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
///     virtual texture preview: lists textures on the left; selecting one extracts and decodes
///     the selected layer to a bitmap in the background, with cancellation and progress reporting.
/// </summary>
public partial class VirtualTexturePreviewViewModel : DisposableViewModel
{
    private readonly VirtualTextureLoader _loader;
    private CancellationTokenSource? _cts;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VirtualTexturePreviewViewModel" /> class.
    /// </summary>
    /// <param name="loader"></param>
    public VirtualTexturePreviewViewModel(VirtualTextureLoader loader)
    {
        _loader = loader;
        Layers =
        [
            .. Enumerable.Range(0, loader.LayerCount)
                .Select(i => $"Layer {i}")
        ];
        foreach (var meta in loader.GetTextures()) Textures.Add(new VirtualTextureItemViewModel(meta));
        SelectedTexture = Textures.FirstOrDefault();
        WeakReferenceMessenger.Default.Register<SearchMessage>(this, (_, message) => OnSearchMessage(message));
    }

    /// <summary>
    ///     Full texture list. Filtering is a view concern: the view applies <c>TextureFilter</c>
    ///     to this collection's collection view, which keeps WPF's ICollectionView out of the view model.
    /// </summary>
    public ObservableCollection<VirtualTextureItemViewModel> Textures { get; } = [];

    /// <summary>
    ///     The list of layers.
    /// </summary>
    public IReadOnlyList<string> Layers { get; }

    /// <summary>
    ///     The currently selected texture.
    /// </summary>
    [ObservableProperty]
    public partial VirtualTextureItemViewModel? SelectedTexture { get; set; }

    /// <summary>
    ///     The current search text.
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    [ObservableProperty]
    private partial string? SearchText { get; set; }

    /// <summary>
    ///     Predicate the view applies to <see cref="Textures" />; null shows every texture. Rebuilt
    ///     whenever <c>SearchText</c> changes, which the view picks up and re-applies.
    /// </summary>
    [ObservableProperty]
    public partial Predicate<object>? TextureFilter { get; private set; }

    /// <summary>
    ///     The index of the currently selected layer.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedLayerIndex { get; set; }

    /// <summary>
    ///     The decoded preview image, platform-agnostic (no WPF types). The view converts it for
    ///     display; this view model owns it and disposes it when replaced or disposed.
    /// </summary>
    [ObservableProperty]
    public partial Image? Preview { get; private set; }

    /// <summary>
    ///     True if the view model is busy loading a preview.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    ///     The status text.
    /// </summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>
    ///     The progress of the current operation, from 0 to 1.
    /// </summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>
    ///     Handles search messages.
    /// </summary>
    /// <param name="message"></param>
    private void OnSearchMessage(SearchMessage message)
    {
        SearchText = string.IsNullOrEmpty(message.Text) ? null : message.Text;
    }

    /// <summary>
    ///     Rebuilds the texture filter when the search text changes.
    /// </summary>
    /// <param name="value"></param>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSearchTextChanged(string? value)
    {
        TextureFilter = string.IsNullOrWhiteSpace(value)
            ? null
            : item => item is VirtualTextureItemViewModel texture &&
                      texture.DisplayName.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

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
            Log.Error(ex, "Failed to preview virtual texture: {Name}", meta.Name);
            StatusText = Strings.TitlePreviewFailed;
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
        var image = await ImageLoader.DecodeDdsAsync(ddsStream);
        if (cts.IsCancellationRequested)
        {
            image?.Dispose();
            return;
        }

        ShowPreview(image);
    }

    /// <summary>
    ///     Shows no data.
    /// </summary>
    private void ShowNoData()
    {
        Preview = null;
        StatusText = Strings.TitleNoDataForLayer;
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
        StatusText = Strings.TitleExtracting;
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
        StatusText = image is null
            ? Strings.TitleDecodeFailed
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

    protected override void Dispose(bool disposing)
    {
        WeakReferenceMessenger.Default.Unregister<SearchMessage>(this);
        base.Dispose(disposing);
        if (!disposing) return;
        _ = _cts?.CancelAsync();
        _loader.Dispose();

        // Detach the view from the image before releasing it.
        Preview = null;
    }
}