using System.Collections.ObjectModel;
using BG3PakViewer.Contracts;
using BG3PakViewer.Services;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Shared.ViewModels;
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
///     <c>.Dialogs</c> (modal dialogs and about data) and <c>.Window</c> (load and external links).
/// </remarks>
internal partial class MainWindowViewModel : DisposableViewModel, IDropTarget
{
    private readonly IDialogService _dialogService;
    private readonly IExportService _exportService;
    private readonly ILogAccessService _logAccessService;
    private readonly IPackageService _packageService;
    private readonly IPreviewService _previewService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly ISettingsManagerService _settingsManagerService;
    private readonly IShellOpenService _shellOpenService;
    private CancellationTokenSource? _cancellationTokenSource;
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
    public MainWindowViewModel(
        IDialogService dialogService,
        IShellOpenService shellOpenService,
        IExportService exportService,
        ILogAccessService logAccessService,
        IPackageService packageService,
        IPreviewService previewService,
        IRecentFilesService recentFilesService,
        ISettingsManagerService settingsManagerService)
    {
        _dialogService = dialogService;
        _shellOpenService = shellOpenService;
        _exportService = exportService;
        _logAccessService = logAccessService;
        _packageService = packageService;
        _previewService = previewService;
        _recentFilesService = recentFilesService;
        _settingsManagerService = settingsManagerService;
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _packageService.Dispose();
        _cancellationTokenSource?.Dispose();
        if (PreviewVm is IDisposable disposable)
            disposable.Dispose();
    }
}