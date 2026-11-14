using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using BG3PakViewer.Locales;
using BG3PakViewer.Messaging;
using BG3PakViewer.Services;
using BG3PakViewer.Shared.Extensions;
using BG3PakViewer.Shared.Models;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using iNKORE.UI.WPF.DragDrop;
using Serilog;

namespace BG3PakViewer.ViewModels;

/// <summary>
///     Package lifetime: opening, validating, loading and tearing down the archive, plus the drop
///     target and the message that asks for a recently opened file to be opened.
/// </summary>
internal partial class MainWindowViewModel
{
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
            if (!Path.GetExtension(file).Equals(".pak", StringComparison.OrdinalIgnoreCase)) return;
            await ValidateAndOpenPackageAsync(file);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading PAK file");
        }
    }

    /// <summary>
    ///     Drag over
    /// </summary>
    /// <param name="dropInfo"></param>
    void IDropTarget.DragOver(IDropInfo dropInfo)
    {
        dropInfo.Effects = DragDropEffects.Copy;
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
            Log.Information("PAK file selection was cancelled");
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
        try
        {
            // The preview view model notifies the UI (its export command raises CanExecuteChanged),
            // so it is released here on the UI thread instead of inside the background work below.
            await DisposePreviewVmAsync();

            // The toolbox index and the extractor hold streams into the package being replaced, so they
            // are released before the package is cleaned up.
            IsVirtualTexturePackage = false;
            await _virtualTextureCatalog.ClearAsync();
            await _virtualTextureExtractor.ClearAsync();

            // The tree is bound to the view, so it may only be replaced from the UI thread: the
            // background work builds the new one and the continuation below publishes it.
            PackageTree = null;
            var tree = await Task.Run(async () =>
            {
                await _packageService.CleanupAsync();
                return await LoadAndBuildTreeAsync(path);
            }, CancellationToken.None);

            if (tree is not null)
            {
                PackageTree = tree;
                _recentFilesService.AddOrUpdateRecentFile(path);
                IsVirtualTexturePackage = DetectVirtualTexturePackage();
            }
            else
            {
                await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.OpenFileFailedMessage,
                    Strings.OpenFileFailedCaption,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            // A failed open must not leave the flag set: HandleReOpenFile would otherwise answer
            // every later attempt with "file loading, please do not repeat the operation" for the
            // rest of the session.
            _isLoading = false;
        }
    }

    /// <summary>
    ///     Loads the package and builds its tree
    /// </summary>
    /// <param name="path">The package to load</param>
    /// <returns>The built tree, or null when the package could not be loaded</returns>
    private async Task<ObservableCollection<PackageEntry>?> LoadAndBuildTreeAsync(string path)
    {
        var success = await _packageService.LoadPackageAsync(path);
        return success ? BuildPackageTree() : null;
    }

    /// <summary>
    ///     Builds the tree from the currently loaded package. Runs on the caller's thread: every
    ///     caller does this on a worker and publishes the result from the UI thread.
    /// </summary>
    /// <param name="searchQuery">The tree search box query.</param>
    /// <returns>The tree, or null when no package is loaded.</returns>
    private ObservableCollection<PackageEntry>? BuildPackageTree(string? searchQuery = null)
    {
        if (!_packageService.IsLoaded) return null;
        var paths = _packageService.GetValidFiles().Select(f => f.Name);
        return PackageTreeBuilder.Build(paths, searchQuery);
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
    ///     Register file message handlers
    /// </summary>
    private void RegisterFileMessageHandlers()
    {
        WeakReferenceMessenger.Default.Register<MainWindowViewModel, ValueChangedMessage<string>, string>(
            this,
            MessageTokens.RecentFileOpened,
            async void (_, m) =>
            {
                try
                {
                    await ValidateAndOpenPackageAsync(m.Value);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error opening file: {FileName}", m.Value);
                }
            });
    }
}