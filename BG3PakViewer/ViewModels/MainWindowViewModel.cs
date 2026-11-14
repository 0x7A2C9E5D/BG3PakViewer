using System.Collections.ObjectModel;
using BG3PakViewer.Contracts;
using BG3PakViewer.Services;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Shared.ViewModels;
using BG3PakViewer.VirtualTextures.Catalog;
using BG3PakViewer.VirtualTextures.Extraction;
using CommunityToolkit.Mvvm.ComponentModel;
using HanumanInstitute.MvvmDialogs;
using iNKORE.UI.WPF.DragDrop;

namespace BG3PakViewer.ViewModels;

/// <summary>
///     Main window view model
/// </summary>
/// <remarks>
///     Split by responsibility across the partial files next to this one:
///     <c>.Package</c> (archive lifetime and file opening), <c>.Search</c> (search commands),
///     <c>.Export</c> (file and folder export), <c>.Preview</c> (preview pane),
///     <c>.VirtualTexture</c> (the virtual texture toolbox), <c>.Dialogs</c> (modal dialogs and about
///     data) and <c>.Window</c> (load and external links).
/// </remarks>
internal partial class MainWindowViewModel : DisposableViewModel, IDropTarget, IAsyncDisposable
{
    private readonly IClipboardService _clipboardService;
    private readonly IDialogService _dialogService;
    private readonly IExportService _exportService;
    private readonly ILogAccessService _logAccessService;
    private readonly IPackageService _packageService;
    private readonly IPreviewExportService _previewExportService;
    private readonly IPreviewService _previewService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly ISettingsManagerService _settingsManagerService;
    private readonly IShellOpenService _shellOpenService;
    private readonly IVirtualTextureCatalog _virtualTextureCatalog;
    private readonly IVirtualTextureExtractor _virtualTextureExtractor;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _catalogReleased;
    private bool _disposed;
    private TaskCompletionSource? _exportCompletion;
    private bool _isLoading;

    /// <summary>
    ///     Main window view model
    /// </summary>
    /// <param name="dialogService"></param>
    /// <param name="shellOpenService"></param>
    /// <param name="exportService"></param>
    /// <param name="logAccessService"></param>
    /// <param name="packageService"></param>
    /// <param name="previewService"></param>
    /// <param name="recentFilesService"></param>
    /// <param name="settingsManagerService"></param>
    /// <param name="virtualTextureCatalog">The index of the package's virtual textures.</param>
    /// <param name="virtualTextureExtractor">The service the toolbox extracts texture layers with.</param>
    /// <param name="previewExportService">The service the toolbox writes its exports to.</param>
    /// <param name="clipboardService">The service the toolbox copies texture names with.</param>
    public MainWindowViewModel(
        IDialogService dialogService,
        IShellOpenService shellOpenService,
        IExportService exportService,
        ILogAccessService logAccessService,
        IPackageService packageService,
        IPreviewService previewService,
        IRecentFilesService recentFilesService,
        ISettingsManagerService settingsManagerService,
        IVirtualTextureCatalog virtualTextureCatalog,
        IVirtualTextureExtractor virtualTextureExtractor,
        IPreviewExportService previewExportService,
        IClipboardService clipboardService)
    {
        _dialogService = dialogService;
        _shellOpenService = shellOpenService;
        _exportService = exportService;
        _logAccessService = logAccessService;
        _packageService = packageService;
        _previewService = previewService;
        _recentFilesService = recentFilesService;
        _settingsManagerService = settingsManagerService;
        _virtualTextureCatalog = virtualTextureCatalog;
        _virtualTextureExtractor = virtualTextureExtractor;
        _previewExportService = previewExportService;
        _clipboardService = clipboardService;
        RegisterFileMessageHandlers();
    }

    private IAppSettings AppSettings => _settingsManagerService.CurrentSettings;

    /// <summary>
    ///     Package tree
    /// </summary>
    // ReSharper disable once MemberCanBeMadeStatic.Global
    [ObservableProperty]
    public partial ObservableCollection<PackageEntry>? PackageTree { get; private set; }

    /// <summary>
    ///     Preview view model
    /// </summary>
    [ObservableProperty]
    public partial object? PreviewVm { get; private set; }

    /// <summary>
    ///     Is update available
    /// </summary>
    // ReSharper disable once MemberCanBeMadeStatic.Global
    [ObservableProperty]
    public partial bool IsUpdateAvailable { get; private set; }

    /// <summary>
    ///     Releases the virtual texture catalog and then the resources the loaded package owns.
    /// </summary>
    /// <remarks>
    ///     The catalog may still be indexing, and stopping it is asynchronous: this is awaited from the
    ///     main window once it has closed, so the UI thread never waits for the index.
    /// </remarks>
    /// <returns>A task that completes when the catalog and the package resources are released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_catalogReleased) return;
        _catalogReleased = true;
        // The extractor and the package service read the package through streams, so the catalog,
        // which may still be reading it, has to be released first.
        await _virtualTextureCatalog.DisposeAsync();
        Dispose();
    }

    /// <summary>
    ///     Releases the extractor, the package service, the cancellation source and the preview view
    ///     model; the catalog is released by <see cref="DisposeAsync" />.
    /// </summary>
    /// <param name="disposing">Whether managed resources are released.</param>
    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose(disposing);
        _virtualTextureExtractor.Dispose();
        _packageService.Dispose();
        _cancellationTokenSource?.Dispose();
        if (PreviewVm is IDisposable disposable)
            disposable.Dispose();
    }
}