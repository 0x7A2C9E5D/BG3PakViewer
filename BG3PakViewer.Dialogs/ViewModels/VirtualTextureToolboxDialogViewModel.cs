using System.Collections.ObjectModel;
using BG3PakViewer.Contracts;
using BG3PakViewer.Shared.ViewModels;
using BG3PakViewer.VirtualTextures.Catalog;
using BG3PakViewer.VirtualTextures.Extraction;
using BG3PakViewer.VirtualTextures.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using Serilog;
using SixLabors.ImageSharp;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     The cross-file virtual texture toolbox: every texture of the package in one searchable list,
///     with the preview of the selected texture and a batch export.
/// </summary>
/// <remarks>
///     The class is split by responsibility: this file holds the state, the search and the preview
///     status, the sibling files load the preview and export the textures.
/// </remarks>
public partial class VirtualTextureToolboxDialogViewModel : SearchFilterViewModel, IModalDialogViewModel
{
    private readonly IAppSettings _appSettings;
    private readonly IVirtualTextureCatalog _catalog;
    private readonly IClipboardService _clipboardService;
    private readonly IDialogService _dialogService;
    private readonly IPreviewExportService _exportService;
    private readonly IVirtualTextureExtractor _extractor;
    private List<VirtualTextureToolboxTextureViewModel> _selectedTextures = [];
    private CancellationTokenSource? _selectionCheckCancellation;
    private bool _selectionHasData;

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
    ///     The texture name or tile set file name the list was opened with, if any.
    /// </summary>
    public string? InitialSearch { get; }

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
    ///     Whether the package's virtual textures are being indexed.
    /// </summary>
    [ObservableProperty]
    public partial bool IsIndexing { get; private set; }

    /// <summary>
    ///     Whether the selected textures are being exported.
    /// </summary>
    [ObservableProperty]
    public partial bool IsExporting { get; private set; }

    /// <summary>
    ///     Whether the preview of the selected texture is being loaded.
    /// </summary>
    [ObservableProperty]
    private partial bool IsPreviewing { get; set; }

    /// <summary>
    ///     The progress of the current operation, from 0 to 100.
    /// </summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>
    ///     The number of tile sets indexed so far.
    /// </summary>
    [ObservableProperty]
    public partial int IndexedCount { get; private set; }

    /// <summary>
    ///     The number of tile sets to index.
    /// </summary>
    [ObservableProperty]
    public partial int IndexTotal { get; private set; }

    /// <summary>
    ///     The number of textures selected in the list.
    /// </summary>
    [ObservableProperty]
    private partial int SelectedCount { get; set; }

    /// <summary>
    ///     Whether indexing, previewing or exporting is in progress.
    /// </summary>
    public bool IsBusy => IsIndexing || IsExporting || IsPreviewing;

    /// <summary>
    ///     The dialog result; the dialog is closed with the close button.
    /// </summary>
    public bool? DialogResult => true;

    /// <summary>
    ///     Updates the textures the export writes from the list selection.
    /// </summary>
    /// <param name="selected">The textures selected in the list.</param>
    public void UpdateSelection(IEnumerable<VirtualTextureToolboxTextureViewModel> selected)
    {
        _selectedTextures = [.. selected];
        SelectedCount = _selectedTextures.Count;
        _ = CheckSelectionAsync();
    }

    /// <summary>
    ///     Finds out whether the selection holds any layer to export, so that a selection which is
    ///     empty in every layer cannot be exported at all.
    /// </summary>
    private async Task CheckSelectionAsync()
    {
        var cts = await BeginSelectionCheckAsync();

        try
        {
            _selectionHasData = await TryAnySelectedLayerHasDataAsync(cts.Token);
        }
        finally
        {
            EndSelectionCheck(cts);
        }
    }

    /// <summary>
    ///     Runs the layer-data check, treating a cancellation and a failure as "no data" so the export
    ///     stays disabled instead of offering files that cannot be written.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true" /> when one selected layer holds data.</returns>
    private async Task<bool> TryAnySelectedLayerHasDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await AnySelectedLayerHasDataAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A newer selection replaced this check.
            return false;
        }
        catch (Exception ex)
        {
            // Failing to prove the selection has data keeps the export disabled, which is the safe side.
            Log.Error(ex, "Failed to check whether the selected textures hold any layer data");
            return false;
        }
    }

    /// <summary>
    ///     Starts a new selection check, cancelling the one it replaces, and keeps the export disabled
    ///     until the check proves the selection holds data.
    /// </summary>
    /// <returns>The cancellation source of the new check.</returns>
    private async Task<CancellationTokenSource> BeginSelectionCheckAsync()
    {
        if (_selectionCheckCancellation is not null) await _selectionCheckCancellation.CancelAsync();

        var cts = new CancellationTokenSource();
        _selectionCheckCancellation = cts;
        // Off until the check proves otherwise: the command must not offer an export that has
        // nothing to write.
        _selectionHasData = false;
        ExportSelectedCommand.NotifyCanExecuteChanged();

        return cts;
    }

    /// <summary>
    ///     Re-enables the export for the check that finished, unless a newer check replaced it already.
    /// </summary>
    /// <param name="cts">The cancellation source of the check that finished.</param>
    private void EndSelectionCheck(CancellationTokenSource cts)
    {
        if (!ReferenceEquals(_selectionCheckCancellation, cts)) return;

        ExportSelectedCommand.NotifyCanExecuteChanged();
        _selectionCheckCancellation = null;
    }

    /// <summary>
    ///     Finds out whether any selected texture holds a layer to export.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true" /> when at least one selected layer holds data.</returns>
    private async Task<bool> AnySelectedLayerHasDataAsync(CancellationToken cancellationToken)
    {
        // Grouped by tile set so the cached one is parsed as few times as possible.
        var textures = _selectedTextures.OrderBy(texture => texture.Entry.GtsPath, StringComparer.Ordinal).ToList();
        for (var layer = 0; layer < VirtualTextureLayers.Names.Count; layer++)
            if (await AnyTextureHasLayerDataAsync(textures, layer, cancellationToken))
                return true;

        return false;
    }

    /// <summary>
    ///     Finds out whether one of the textures holds a layer to export at the given layer index.
    /// </summary>
    /// <param name="textures">The selected textures, ordered by tile set.</param>
    /// <param name="layer">The layer index to check.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true" /> when one texture holds data in that layer.</returns>
    private async Task<bool> AnyTextureHasLayerDataAsync(
        IReadOnlyList<VirtualTextureToolboxTextureViewModel> textures, int layer, CancellationToken cancellationToken)
    {
        foreach (var texture in textures.Where(texture =>
                     layer < Math.Min(texture.Entry.LayerCount, VirtualTextureLayers.Names.Count)))
            if (await _extractor.HasLayerDataAsync(texture.Entry, layer, cancellationToken))
                return true;

        return false;
    }

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

        // Detach the view from the image before releasing it.
        Preview = null;
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnIsIndexingChanged(bool value)
    {
        ExportSelectedCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsBusy));
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnIsExportingChanged(bool value)
    {
        ExportSelectedCommand.NotifyCanExecuteChanged();
        CancelExportCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsBusy));
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnIsPreviewingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBusy));
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedCountChanged(int value)
    {
        ExportSelectedCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Indexes the package's tile sets, or reuses the index the catalog already holds.
    /// </summary>
    private async Task IndexAsync()
    {
        if (_catalog.IsLoaded)
        {
            BuildRows();
            return;
        }

        IsIndexing = true;
        try
        {
            var progress = new Progress<VirtualTextureIndexProgress>(value =>
            {
                IndexedCount = value.Indexed;
                IndexTotal = value.Total;
            });
            await _catalog.LoadAsync(progress);
            BuildRows();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to index virtual textures for the toolbox");
        }
        finally
        {
            IsIndexing = false;
        }
    }

    /// <summary>
    ///     Turns the indexed textures into rows, applies the search the dialog was opened with and
    ///     selects its first match.
    /// </summary>
    private void BuildRows()
    {
        Textures.Clear();
        foreach (var entry in _catalog.Entries)
            Textures.Add(new VirtualTextureToolboxTextureViewModel(entry, _clipboardService));

        if (!string.IsNullOrWhiteSpace(InitialSearch))
            SearchText = InitialSearch;
        SelectedRow = Textures.FirstOrDefault(texture =>
            string.IsNullOrWhiteSpace(InitialSearch) || Matches(texture, InitialSearch));
        Log.Information("Virtual texture toolbox loaded {TextureCount} textures", Textures.Count);
    }

    /// <summary>
    ///     Builds the filter the list view applies: a case-insensitive substring match on the texture
    ///     name or on the tile set file the texture comes from.
    /// </summary>
    /// <param name="searchText">The submitted search text.</param>
    /// <returns>The predicate, or <see langword="null" /> when everything is shown.</returns>
    protected override Predicate<object>? BuildFilter(string? searchText)
    {
        return string.IsNullOrWhiteSpace(searchText)
            ? null
            : item => item is VirtualTextureToolboxTextureViewModel texture && Matches(texture, searchText);
    }

    /// <summary>
    ///     Whether a texture matches the search text, by texture name or by the tile set file it comes from,
    ///     so searching a tile set name narrows the list to the textures of that tile set.
    /// </summary>
    /// <param name="texture">The texture to test.</param>
    /// <param name="searchText">The submitted search text.</param>
    /// <returns><see langword="true" /> when the texture matches.</returns>
    private static bool Matches(VirtualTextureToolboxTextureViewModel texture, string searchText)
    {
        return texture.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               texture.SourceFile.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Applies the text the search box submits, with Enter or with the query icon.
    /// </summary>
    /// <param name="query">The text typed in the search box.</param>
    [RelayCommand]
    private void Search(string? query)
    {
        SearchText = query ?? string.Empty;
    }

    /// <summary>
    ///     Clears the filter as soon as the search box is emptied, without waiting for a submit.
    /// </summary>
    /// <param name="query">The text the search box now holds.</param>
    [RelayCommand]
    private void ClearSearch(string? query)
    {
        if (!string.IsNullOrWhiteSpace(query)) return;
        SearchText = string.Empty;
    }
}