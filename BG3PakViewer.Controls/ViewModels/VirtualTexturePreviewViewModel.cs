using System.Collections.ObjectModel;
using BG3PakViewer.Contracts;
using BG3PakViewer.Loader;
using BG3PakViewer.Shared.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using HanumanInstitute.MvvmDialogs;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     virtual texture preview: lists textures on the left; selecting one extracts and decodes
///     the selected layer to a bitmap in the background, with cancellation and progress reporting.
/// </summary>
/// <remarks>
///     The class is split by responsibility: this file holds the state of the view model, the
///     sibling files load the preview and export the layers.
/// </remarks>
public partial class VirtualTexturePreviewViewModel : SearchFilterViewModel
{
    /// <summary>
    ///     Virtual texture layers are fixed: Albedo, Normal and Physical.
    /// </summary>
    private const int SupportedLayerCount = 3;

    /// <summary>
    ///     Layer names, kept in sync with the layer selector of the view; used to name exported files.
    /// </summary>
    private static readonly string[] LayerNames = ["Albedo", "Normal", "Physical"];

    private readonly IAppSettings _appSettings;
    private readonly IDialogService _dialogService;
    private readonly IPreviewExportService _exportService;
    private readonly VirtualTextureLoader _loader;
    private CancellationTokenSource? _cts;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VirtualTexturePreviewViewModel" /> class.
    /// </summary>
    /// <param name="loader"></param>
    /// <param name="dialogService">The service used to pick the export folder and report the result.</param>
    /// <param name="appSettings">The settings holding the default export directory.</param>
    /// <param name="exportService">The service that writes the exported layer files.</param>
    /// <param name="clipboardService">The service used to copy texture names.</param>
    public VirtualTexturePreviewViewModel(VirtualTextureLoader loader, IDialogService dialogService,
        IAppSettings appSettings, IPreviewExportService exportService, IClipboardService clipboardService)
    {
        _loader = loader;
        _dialogService = dialogService;
        _appSettings = appSettings;
        _exportService = exportService;
        if (loader.LayerCount > SupportedLayerCount)
            throw new InvalidOperationException(
                $"Unsupported virtual texture: {loader.LayerCount} layers found, " +
                $"but at most {SupportedLayerCount} (Albedo, Normal, Physical) are supported.");
        foreach (var meta in loader.GetTextures())
            Textures.Add(new VirtualTextureItemViewModel(meta, clipboardService));
        SelectedTexture = Textures.FirstOrDefault();
    }

    /// <summary>
    ///     Full texture list. Filtering is a view concern: the view applies <c>ItemFilter</c>
    ///     to this collection's collection view, which keeps WPF's ICollectionView out of the view model.
    /// </summary>
    public ObservableCollection<VirtualTextureItemViewModel> Textures { get; } = [];

    /// <summary>
    ///     The currently selected texture.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial VirtualTextureItemViewModel? SelectedTexture { get; set; }

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
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial Image? Preview { get; private set; }

    /// <summary>
    ///     True if the view model is busy loading a preview.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>
    ///     The resource key, or an untranslated source string (the image dimensions), that the view
    ///     resolves through I18N. The view model never holds localized UI text, so a culture change
    ///     refreshes the whole status line from one binding.
    /// </summary>
    [ObservableProperty]
    public partial string? StatusKey { get; private set; } = string.Empty;

    /// <summary>
    ///     The progress of the current operation, from 0 to 1.
    /// </summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>
    ///     Builds the texture filter for the given search text.
    /// </summary>
    /// <param name="searchText"></param>
    /// <returns></returns>
    protected override Predicate<object>? BuildFilter(string? searchText)
    {
        return string.IsNullOrWhiteSpace(searchText)
            ? null
            : item => item is VirtualTextureItemViewModel texture &&
                      texture.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _ = _cts?.CancelAsync();
        _loader.Dispose();

        // Detach the view from the image before releasing it.
        Preview = null;
    }
}