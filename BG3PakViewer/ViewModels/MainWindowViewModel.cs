using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using BG3PakViewer.Contracts;
using BG3PakViewer.Dialogs.ViewModels;
using BG3PakViewer.Locales;
using BG3PakViewer.Messaging;
using BG3PakViewer.Services;
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

    public bool IsExporting { get; private set; }

    // ReSharper disable once MemberCanBeMadeStatic.Global
    [ObservableProperty] public partial ObservableCollection<PackageEntry>? PackageTree { get; private set; }

    [ObservableProperty] public partial object? PreviewVm { get; private set; }

    // ReSharper disable once MemberCanBeMadeStatic.Global
    [ObservableProperty] public partial bool IsUpdateAvailable { get; private set; }

    void IDropTarget.DragOver(IDropInfo dropInfo)
    {
        dropInfo.Effects = DragDropEffects.Copy;
    }

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

    [RelayCommand]
    private async Task SearchAsync(string query)
    {
        if (!_packageService.IsLoaded) return;

        await Task.Run(() =>
        {
            Log.Information("Searching for '{Query}'...", query);
            PackageTree = _packageService.BuildTree(query);
            Log.Information("Search completed.");
        }, CancellationToken.None);
    }

    [RelayCommand]
    private async Task ClearSearchAsync(string query)
    {
        if (!_packageService.IsLoaded || !string.IsNullOrWhiteSpace(query)) return;
        await Task.Run(() => { PackageTree = _packageService.BuildTree(); }, CancellationToken.None);
        Log.Information("Search query cleared.");
    }

    private DesktopDialogStorageFolder? GetDefaultOpenLocation()
    {
        return !string.IsNullOrWhiteSpace(AppSettings.DefaultOpenDirectory)
            ? new DesktopDialogStorageFolder(AppSettings.DefaultOpenDirectory)
            : null;
    }

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

    private async Task ValidateAndOpenPackageAsync(string path)
    {
        if (!(await HandleIsExportingFiles()
              && await HandleReOpenFile(path))) return;
        await OpenPackageAsync(path);
    }

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
            WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string>(string.Empty),
                MessageTokens.OpenFileFailed);
        _isLoading = false;
    }

    private async Task CleanupCurrentPackageAsync()
    {
        await DisposePreviewVmAsync();
        PreviewVm = null;
        PackageTree = null;
        await _packageService.CleanupAsync();
    }

    private async Task DisposePreviewVmAsync()
    {
        if (PreviewVm is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
    }

    private async Task<bool> LoadAndBuildTreeAsync(string path)
    {
        var success = await _packageService.LoadPackageAsync(path);
        if (!success) return false;
        PackageTree = _packageService.BuildTree();
        return true;
    }

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
        HandleExportResult(success);
    }

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
            HandleExportResult(success);
    }


    private static void HandleExportResult(bool success)
    {
        if (success)
        {
            WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string>(string.Empty),
                MessageTokens.ExportCompleted);
            Log.Information("Export completed.");
        }
        else
        {
            WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string>(string.Empty),
                MessageTokens.ExportFailed);
            Log.Warning("Failed to export file.");
        }
    }

    private DesktopDialogStorageFolder? GetDefaultExportLocation()
    {
        return !string.IsNullOrWhiteSpace(AppSettings.DefaultExportDirectory)
            ? new DesktopDialogStorageFolder(
                AppSettings.DefaultExportDirectory)
            : null;
    }

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

    private async Task ClearPreviewAsync()
    {
        await DisposePreviewVmAsync();
        PreviewVm = null;
    }

    [RelayCommand]
    private async Task ShowLogDialog()
    {
        using var viewModel = new LogDialogViewModel(_logAccessService, _shellOpenService);
        await _dialogService.ShowDialogAsync(this, viewModel);
    }

    [RelayCommand]
    private async Task ShowRecentDialog()
    {
        using var viewModel = new RecentDialogViewModel(_recentFilesService);
        await _dialogService.ShowDialogAsync(this, viewModel);
    }

    [RelayCommand]
    private async Task ShowSettingsDialog()
    {
        var viewModel = new SettingsDialogViewModel(_settingsManagerService, _dialogService);
        await _dialogService.ShowDialogAsync(this, viewModel);
    }

    [RelayCommand]
    private async Task ShowAbout()
    {
        var viewModel = new AboutDialogViewModel(BuildAboutData());
        await _dialogService.ShowDialogAsync(this, viewModel);
    }

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

    private async Task<bool> HandleReOpenFile(string fileName)
    {
        if (_isLoading)
        {
            WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string>(string.Empty),
                MessageTokens.FileLoadingDuplicate);
            return false;
        }

        if (PackageTree is null) return true;
        return await WeakReferenceMessenger.Default.Send(
            new AsyncRequestMessage<string, bool>(fileName),
            MessageTokens.ReOpenFile);
    }

    private async Task<bool> HandleIsExportingFiles()
    {
        if (!IsExporting) return true;
        if (!await WeakReferenceMessenger.Default
                .Send(new AsyncRequestMessage<bool>(), MessageTokens.CancelExport)) return false;
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

    [RelayCommand]
    private void OpenNexusMods()
    {
        _shellOpenService.Open(AppSettings.NexusModUrl);
        Log.Information("NexusMods opened.");
    }

    [RelayCommand]
    private async Task WindowLoadedAsync()
    {
        IsUpdateAvailable = await Ioc.Default.GetRequiredService<ICheckUpdateService>().CheckUpdate();
    }
}