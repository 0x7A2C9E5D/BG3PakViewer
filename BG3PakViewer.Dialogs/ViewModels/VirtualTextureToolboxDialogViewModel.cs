using System.Collections.ObjectModel;
using BG3PakViewer.Contracts;
using BG3PakViewer.Shared.ViewModels;
using BG3PakViewer.VirtualTextures.Catalog;
using BG3PakViewer.VirtualTextures.Extraction;
using CommunityToolkit.Mvvm.ComponentModel;
using HanumanInstitute.MvvmDialogs;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     The cross-file virtual texture toolbox: every texture of the package in one searchable list,
///     with the preview of the selected texture and a batch export.
/// </summary>
/// <remarks>
///     The class is split by responsibility: this file holds the shared state, the constructor and
///     the disposal of the running operations; <c>Index</c> builds the list, <c>Search</c> filters it,
///     <c>Selection</c> tracks the rows to export, <c>Preview</c> loads the selected layer and
///     <c>Export</c> writes the files.
/// </remarks>
public partial class VirtualTextureToolboxDialogViewModel : SearchFilterViewModel, IModalDialogViewModel
{
    private readonly IAppSettings _appSettings;
    private readonly IVirtualTextureCatalog _catalog;
    private readonly IClipboardService _clipboardService;
    private readonly IDialogService _dialogService;
    private readonly IPreviewExportService _exportService;
    private readonly IVirtualTextureExtractor _extractor;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VirtualTextureToolboxDialogViewModel" /> class.
    /// </summary>
    /// <param name="catalog">The index of the package's virtual textures.</param>
    /// <param name="extractor">The service that extracts the selected texture layers.</param>
    /// <param name="exportService">The service the selected textures are written by.</param>
    /// <param name="appSettings">The settings holding the default export directory.</param>
    /// <param name="dialogService">The service used to pick the export folder and report the result.</param>
    /// <param name="clipboardService">The service used to copy texture names.</param>
    /// <param name="initialSearch">
    ///     The texture name or tile set file name to search and select after indexing, or
    ///     <see langword="null" /> to show the whole list.
    /// </param>
    public VirtualTextureToolboxDialogViewModel(IVirtualTextureCatalog catalog, IVirtualTextureExtractor extractor,
        IPreviewExportService exportService, IAppSettings appSettings, IDialogService dialogService,
        IClipboardService clipboardService, string? initialSearch = null)
    {
        _catalog = catalog;
        _extractor = extractor;
        _exportService = exportService;
        _appSettings = appSettings;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        InitialSearch = initialSearch;
        _ = IndexAsync();
    }

    /// <summary>
    ///     The textures of the package, in index order; the list view filters them through
    ///     <see cref="SearchFilterViewModel.ItemFilter" />.
    /// </summary>
    public ObservableCollection<VirtualTextureToolboxTextureViewModel> Textures { get; } = [];

    /// <summary>
    ///     The texture whose preview is shown.
    /// </summary>
    [ObservableProperty]
    public partial VirtualTextureToolboxTextureViewModel? SelectedRow { get; set; }

    /// <summary>
    ///     The index of the layer the preview shows.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedLayerIndex { get; set; }

    /// <summary>
    ///     The progress of the current operation, from 0 to 100.
    /// </summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>
    ///     Whether indexing, previewing or exporting is in progress.
    /// </summary>
    public bool IsBusy => IsIndexing || IsExporting || IsPreviewing;

    /// <summary>
    ///     The dialog result; the dialog is closed with the close button.
    /// </summary>
    public bool? DialogResult => true;

    /// <summary>
    ///     Cancels previewing and exporting, and detaches the image from the view before it is released.
    /// </summary>
    /// <param name="disposing">Whether managed resources are released.</param>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _ = _previewCancellation?.CancelAsync();
        _ = _exportCancellation?.CancelAsync();
        _ = _selectionCheckCancellation?.CancelAsync();
        _ = _indexCancellation?.CancelAsync();

        // Detach the view from the image before releasing it.
        Preview = null;
    }
}
