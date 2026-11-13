using System.Collections.ObjectModel;
using BG3PakViewer.Contracts;
using BG3PakViewer.Shared.ViewModels;
using BG3PakViewer.VirtualTextures.Catalog;
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
    private readonly IVirtualTextureExtractor _extractor;
    private readonly IClipboardService _clipboardService;
    private readonly IDialogService _dialogService;
    private readonly IPreviewExportService _exportService;
    private List<VirtualTextureToolboxTextureViewModel> _selectedTextures = [];

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
        SelectedRow = Textures.FirstOrDefault(row =>
            string.IsNullOrWhiteSpace(InitialSearch) || Matches(row, InitialSearch));
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
            : item => item is VirtualTextureToolboxTextureViewModel row && Matches(row, searchText);
    }

    /// <summary>
    ///     Whether a row matches the search text, by texture name or by the tile set file it comes from,
    ///     so searching a tile set name narrows the list to the textures of that tile set.
    /// </summary>
    /// <param name="row">The row to test.</param>
    /// <param name="searchText">The submitted search text.</param>
    /// <returns><see langword="true" /> when the row matches.</returns>
    private static bool Matches(VirtualTextureToolboxTextureViewModel row, string searchText)
    {
        return row.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               row.SourceFile.Contains(searchText, StringComparison.OrdinalIgnoreCase);
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