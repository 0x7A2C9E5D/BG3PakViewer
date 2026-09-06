using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using BG3PakViewer.Contracts;
using BG3PakViewer.Dialogs.ViewModels;
using BG3PakViewer.Locales;
using BG3PakViewer.Messaging;
using BG3PakViewer.Services;
using BG3PakViewer.Shared.Extensions;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Shared.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using iNKORE.UI.WPF.DragDrop;
using Microsoft.Extensions.DependencyModel;
using Serilog;

namespace BG3PakViewer.ViewModels;

/// <summary>
///     Main window view model
/// </summary>
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
    ///     Is exporting
    /// </summary>
    public bool IsExporting { get; private set; }

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
    ///     Is loading
    /// </summary>
    /// <param name="dropInfo"></param>
    void IDropTarget.DragOver(IDropInfo dropInfo)
    {
        dropInfo.Effects = DragDropEffects.Copy;
    }

    /// <summary>
    ///     Drop
    /// </summary>
    /// <param name="dropInfo"></param>
    async void IDropTarget.Drop(IDropInfo dropInfo)
    {
        try
        {
            if (dropInfo.Data is not IDataObject data) return;
            var files = data.GetData(DataFormats.FileDrop) as string[];
            if (!(files?.Length > 0)) return;
            var file = files[0];
            if (Path.GetExtension(file) is not ".pak") return;
            await ValidateAndOpenPackageAsync(file);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading PAK file.");
        }
    }

    /// <summary>
    ///     Broadcasts the search query so the active preview (virtual texture list / localization
    ///     grid) handles it via the messenger; with no preview open it searches the package tree.
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync(string query)
    {
        WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string?>(query), MessageTokens.SearchQueryChanged);
        if (PreviewVm is not null) return;

        if (!_packageService.IsLoaded) return;

        await Task.Run(() =>
        {
            Log.Information("Searching for '{Query}'...", query);
            PackageTree = _packageService.BuildTree(query);
            Log.Information("Search completed.");
        }, CancellationToken.None);
    }

    /// <summary>
    ///     Only clears the search state (full list / full tree) when the text was emptied.
    /// </summary>
    [RelayCommand]
    private async Task ClearSearchAsync(string query)
    {
        if (!string.IsNullOrWhiteSpace(query)) return;
        WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string?>(null), MessageTokens.SearchQueryChanged);
        if (PreviewVm is not null) return;

        if (!_packageService.IsLoaded) return;
        await Task.Run(() => { PackageTree = _packageService.BuildTree(); }, CancellationToken.None);
        Log.Information("Search query cleared.");
    }

    /// <summary>
    ///     Get default open location
    /// </summary>
    /// <returns></returns>
    private DesktopDialogStorageFolder? GetDefaultOpenLocation()
    {
        return !string.IsNullOrWhiteSpace(AppSettings.DefaultOpenDirectory)
            ? new DesktopDialogStorageFolder(AppSettings.DefaultOpenDirectory)
            : null;
    }

    /// <summary>
    ///     Open pak async
    /// </summary>
    [RelayCommand]
    private async Task OpenPakAsync()
    {
        var storageFile = await _dialogService
            .ShowOpenFileDialogAsync(this, new OpenFileDialogSettings
            {
                SuggestedStartLocation = GetDefaultOpenLocation(),
                Filters = [new FileFilter(Strings.PakFile, ".pak")],
                Title = Strings.SelectPak
            });
        if (storageFile != null)
            await ValidateAndOpenPackageAsync(storageFile.LocalPath);
        else
            Log.Information("PAK file selection was cancelled.");
    }

    /// <summary>
    ///     Validate and open package async
    /// </summary>
    /// <param name="path"></param>
    private async Task ValidateAndOpenPackageAsync(string path)
    {
        if (!(await HandleIsExportingFiles()
              && await HandleReOpenFile())) return;
        await OpenPackageAsync(path);
    }

    /// <summary>
    ///     Open package async
    /// </summary>
    /// <param name="path"></param>
    private async Task OpenPackageAsync(string path)
    {
        _isLoading = true;
        Log.Information("Opening PAK file: {Path}", path);
        var success = await Task.Run(async () =>
        {
            await CleanupCurrentPackageAsync();
            return await LoadAndBuildTreeAsync(path);
        }, CancellationToken.None);
        if (success)
            _recentFilesService.AddOrUpdateRecentFile(path);
        else
            await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.OpenFileFailedMessage,
                Strings.OpenFileFailedCaption,
                MessageBoxIcon.Error);
        _isLoading = false;
    }

    /// <summary>
    ///     Cleanup current package async
    /// </summary>
    private async Task CleanupCurrentPackageAsync()
    {
        await DisposePreviewVmAsync();
        PreviewVm = null;
        PackageTree = null;
        await _packageService.CleanupAsync();
    }

    /// <summary>
    ///     Dispose preview vm async
    /// </summary>
    private async Task DisposePreviewVmAsync()
    {
        if (PreviewVm is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
    }

    /// <summary>
    ///     Load and build tree async
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private async Task<bool> LoadAndBuildTreeAsync(string path)
    {
        var success = await _packageService.LoadPackageAsync(path);
        if (!success) return false;
        PackageTree = _packageService.BuildTree();
        return true;
    }

    /// <summary>
    ///     Export async
    /// </summary>
    /// <param name="toSave"></param>
    [RelayCommand]
    private async Task ExportAsync(object toSave)
    {
        if (toSave is not PackageEntry node || !_packageService.IsLoaded)
            return;
        IsExporting = true;
        try
        {
            if (node.IsFolder)
                await ExportFolderAsync(node);
            else
                await ExportFileAsync(node);
        }
        finally
        {
            IsExporting = false;
        }
    }

    /// <summary>
    ///     Export file async
    /// </summary>
    /// <param name="node"></param>
    private async Task ExportFileAsync(PackageEntry node)
    {
        var filters = _exportService.GetExportFilters(node.Name, node.FileExtension);
        var storageFile = await _dialogService.ShowSaveFileDialogAsync(this, new SaveFileDialogSettings
        {
            Title = Strings.SaveFile,
            Filters = filters is { Length: > 0 } ? filters : [],
            SuggestedFileName = filters is { Length: > 0 }
                ? Path.GetFileNameWithoutExtension(node.Name)
                : Path.GetFileName(node.Name),
            SuggestedStartLocation = GetDefaultExportLocation()
        });
        if (storageFile == null)
        {
            Log.Information("Export file selection was cancelled.");
            return;
        }

        var success = await _exportService.ExportFileAsync(node, storageFile.LocalPath);
        await HandleExportResultAsync(success);
    }

    /// <summary>
    ///     Export folder async
    /// </summary>
    /// <param name="node"></param>
    private async Task ExportFolderAsync(PackageEntry node)
    {
        var storageFolder = await _dialogService.ShowOpenFolderDialogAsync(this, new OpenFolderDialogSettings
        {
            SuggestedStartLocation = GetDefaultExportLocation()
        });
        if (storageFolder == null)
        {
            Log.Information("Export folder selection was cancelled.");
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        var success =
            await _exportService.ExportFolderAsync(node, storageFolder.LocalPath, _cancellationTokenSource.Token);
        if (_cancellationTokenSource.IsCancellationRequested)
            Log.Information("Folder export was cancelled.");
        else
            await HandleExportResultAsync(success);
    }

    /// <summary>
    ///     Handle export result async
    /// </summary>
    /// <param name="success"></param>
    private async Task HandleExportResultAsync(bool success)
    {
        if (success)
        {
            await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.ExportCompleted, Strings.ExportCompleted,
                MessageBoxIcon.Success);
            Log.Information("Export completed.");
        }
        else
        {
            await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.ExportFailedMessage,
                Strings.ExportFailedCaption,
                MessageBoxIcon.Error);
            Log.Warning("Failed to export file.");
        }
    }

    /// <summary>
    ///     Get default export location
    /// </summary>
    /// <returns></returns>
    private DesktopDialogStorageFolder? GetDefaultExportLocation()
    {
        return !string.IsNullOrWhiteSpace(AppSettings.DefaultExportDirectory)
            ? new DesktopDialogStorageFolder(
                AppSettings.DefaultExportDirectory)
            : null;
    }

    /// <summary>
    ///     Preview async
    /// </summary>
    /// <param name="toPreview"></param>
    [RelayCommand]
    private async Task PreviewAsync(object toPreview)
    {
        if (toPreview is not PackageEntry node || !_packageService.IsLoaded)
            return;
        if (node.IsFolder)
        {
            await ClearPreviewAsync();
            return;
        }

        await ShowPreviewAsync(node);
    }

    /// <summary>
    ///     Show preview async
    /// </summary>
    /// <param name="node"></param>
    private async Task ShowPreviewAsync(PackageEntry node)
    {
        try
        {
            Log.Information("Creating preview for: {Path}", node.FullPath);
            var viewModel = await _previewService.CreatePreviewViewModelAsync(node);
            await DisposePreviewVmAsync();
            PreviewVm = viewModel;
            if (viewModel != null) Log.Debug("Preview displayed successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error showing preview for: {Path}", node.FullPath);
            await ClearPreviewAsync();
        }
    }

    /// <summary>
    ///     Clear preview async
    /// </summary>
    private async Task ClearPreviewAsync()
    {
        await DisposePreviewVmAsync();
        PreviewVm = null;
    }

    /// <summary>
    ///     Show log dialog
    /// </summary>
    [RelayCommand]
    private Task ShowLogDialog()
    {
        return ShowDialogWithLoggingAsync(
            () => new LogDialogViewModel(_logAccessService, _shellOpenService),
            "Failed to show the log dialog.");
    }

    /// <summary>
    ///     Show recent dialog
    /// </summary>
    [RelayCommand]
    private Task ShowRecentDialog()
    {
        return ShowDialogWithLoggingAsync(
            () => new RecentDialogViewModel(_recentFilesService, _dialogService),
            "Failed to show the recent files dialog.");
    }

    /// <summary>
    ///     Show settings dialog
    /// </summary>
    [RelayCommand]
    private Task ShowSettingsDialog()
    {
        return ShowDialogWithLoggingAsync(
            () => new SettingsDialogViewModel(_settingsManagerService, _dialogService),
            "Failed to show the settings dialog.");
    }

    /// <summary>
    ///     Show about dialog
    /// </summary>
    [RelayCommand]
    private Task ShowAbout()
    {
        return ShowDialogWithLoggingAsync(
            () => new AboutDialogViewModel(BuildAboutData()),
            "Failed to show the about dialog.");
    }

    /// <summary>
    ///     Shows a dialog created by the given factory, disposing its view model when it supports
    ///     <see cref="IDisposable" />, and logs any failure with the given message.
    /// </summary>
    /// <typeparam name="TViewModel"></typeparam>
    /// <param name="viewModelFactory"></param>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    private async Task ShowDialogWithLoggingAsync<TViewModel>(
        Func<TViewModel> viewModelFactory,
        string errorMessage)
        where TViewModel : class, IModalDialogViewModel
    {
        TViewModel? viewModel = null;
        try
        {
            viewModel = viewModelFactory();
            await _dialogService.ShowDialogAsync(this, viewModel);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{Message}", errorMessage);
        }
        finally
        {
            if (viewModel is IDisposable disposable) disposable.Dispose();
        }
    }

    /// <summary>
    ///     Build about data
    /// </summary>
    /// <returns></returns>
    private static Dictionary<string, object?> BuildAboutData()
    {
        return new Dictionary<string, object?>
        {
            { "Version", ThisAssembly.AssemblyInformationalVersion },
            { "BuildTime", BuildInformation.BuildAt.ToLocalTime() },
            { "OS", $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})" },
            { "Runtime", RuntimeInformation.FrameworkDescription },
            {
                "Package", DependencyContext.Default?
                    .RuntimeLibraries.Where(x => x.Type == "package")
                    .OrderBy(x => x.Name)
            }
        };
    }

    /// <summary>
    ///     Register file message handlers
    /// </summary>
    private void RegisterFileMessageHandlers()
    {
        WeakReferenceMessenger.Default.Register<MainWindowViewModel, AsyncRequestMessage<string, bool>, string>(
            this,
            MessageTokens.RecentFileOpened,
            async void (_, m) =>
            {
                try
                {
                    await ValidateAndOpenPackageAsync(m.Request);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error opening file: {FileName}", m.Request);
                }
            });
    }

    /// <summary>
    ///     Handle reopen file
    /// </summary>
    /// <returns></returns>
    private async Task<bool> HandleReOpenFile()
    {
        if (_isLoading)
        {
            await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.FileLoadingDuplicateMessage,
                Strings.FileLoadingDuplicateCaption, MessageBoxIcon.Warning);
            return false;
        }

        if (PackageTree is null) return true;
        return await _dialogService.ShowMessageBoxConfirmAsync(this, Strings.ReOpenFileMessage,
            Strings.ReOpenFileCaption);
    }

    /// <summary>
    ///     Handle is exporting files
    /// </summary>
    /// <returns></returns>
    private async Task<bool> HandleIsExportingFiles()
    {
        if (!IsExporting) return true;
        if (!await _dialogService.ShowMessageBoxConfirmAsync(this, Strings.CancelExportOperationMessage,
                Strings.CancelExportOperationCaption, MessageBoxIcon.Warning)) return false;
        await _cancellationTokenSource?.CancelAsync()!;
        IsExporting = false;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _packageService.Dispose();
        _cancellationTokenSource?.Dispose();
        if (PreviewVm is IDisposable disposable)
            disposable.Dispose();
    }

    /// <summary>
    ///     Open nexus mods
    /// </summary>
    [RelayCommand]
    private void OpenNexusMods()
    {
        _shellOpenService.Open(AppSettings.NexusModUrl);
        Log.Information("NexusMods opened.");
    }

    /// <summary>
    ///     Window loaded async
    /// </summary>
    [RelayCommand]
    private async Task WindowLoadedAsync()
    {
        IsUpdateAvailable = await Ioc.Default.GetRequiredService<ICheckUpdateService>().CheckUpdate();
    }
}