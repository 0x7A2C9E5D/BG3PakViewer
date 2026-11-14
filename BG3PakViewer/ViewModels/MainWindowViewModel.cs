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
    private bool _disposeStarted;
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
    ///     Cancels the running export, then disposes the extractor and the loaded package in that order.
    /// </summary>
    /// <remarks>
    ///     The export may still be reading the package, and stopping it is asynchronous: this is
    ///     awaited from the main window once it has closed, so the UI thread never waits for it.
    /// </remarks>
    /// <returns>A task that completes when the package resources are released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposeStarted) return;
        _disposeStarted = true;
        // A running export reads the package through streams of its own, so it is canceled and awaited
        // here exactly like on the open-package path. The wait has to live in this asynchronous method:
        // the export's continuation needs the UI thread, which a synchronous Dispose would block.
        await CancelAndWaitForExportAsync();
        // The extractor may still hold a tileset parsed from this package, so it must go before the
        // package that provided its data.
        await _virtualTextureExtractor.DisposeAsync();
        await _packageService.DisposeAsync();
        Dispose();
    }

    /// <summary>
    ///     Releases the cancellation source and the preview view model; the extractor and the package
    ///     service are released by <see cref="DisposeAsync" />. The catalog holds no resources and needs
    ///     no disposal.
    /// </summary>
    /// <param name="disposing">Whether managed resources are released.</param>
    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose(disposing);
        _cancellationTokenSource?.Dispose();
        if (PreviewVm is IDisposable disposable)
            disposable.Dispose();
    }
}
