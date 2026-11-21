using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using BG3PakViewer.Locales;
using BG3PakViewer.Messaging;
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
    ///     Tracks the tree selection.
    /// </summary>
    /// <param name="item">The newly selected tree item, or null when the selection was cleared.</param>
    [RelayCommand]
    private void SelectItem(object? item)
    {
        _state.SetSelection(item is PackageEntry);
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
    /// <remarks>
    ///     All callers (open button, drag-and-drop, recent-file message) run on the UI thread, so the
    ///     load-state check and the pending field need no synchronization. A request that arrives
    ///     while a load is still running is deferred: only the newest one is kept, and it starts as
    ///     soon as the current load finishes.
    /// </remarks>
    /// <param name="path"></param>
    private async Task ValidateAndOpenPackageAsync(string path)
    {
        if (!await HandleIsExportingFiles()) return;
        if (_state.IsLoading)
        {
            _pendingOpenPath = path;
            Log.Information("Opening {Path} deferred until the current load finishes", path);
            return;
        }

        _state.BeginLoad();
        var success = false;
        try
        {
            success = await OpenAndDrainDeferredAsync(path);
        }
        finally
        {
            _state.EndLoad(success);
        }
    }

    /// <summary>
    ///     Opens the given package, then the newest request that arrived while it was loading.
    /// </summary>
    /// <param name="path">The package file to open.</param>
    /// <returns>Whether the last package of the drain loaded successfully.</returns>
    private async Task<bool> OpenAndDrainDeferredAsync(string path)
    {
        var success = await OpenPackageAsync(path);
        while (_pendingOpenPath is { } next)
        {
            _pendingOpenPath = null;
            success = await OpenPackageAsync(next);
        }

        return success;
    }

    /// <summary>
    ///     Replaces the loaded package and rebuilds the tree bound to the view.
    /// </summary>
    /// <remarks>
    ///     Only the tree is published from the UI thread; loading and building it happen on a worker.
    /// </remarks>
    /// <param name="path">The package file to open.</param>
    /// <returns>Whether the package loaded and its tree was published.</returns>
    private async Task<bool> OpenPackageAsync(string path)
    {
        Log.Information("Opening PAK file: {Path}", path);
        // The preview view model notifies the UI (its export command raises CanExecuteChanged),
        // so it is released here on the UI thread instead of inside the background work below.
        await DisposePreviewVmAsync();

        IsVirtualTexturePackage = false;

        // The tree is bound to the view, so it may only be replaced from the UI thread: the
        // background work builds the new one and the continuation below publishes it.
        PackageTree = null;
        var tree = await Task.Run(() => LoadAndBuildTreeAsync(path), CancellationToken.None);

        if (tree is not null)
        {
            PackageTree = tree;
            _recentFilesService.AddOrUpdateRecentFile(path);
            IsVirtualTexturePackage = await DetectVirtualTexturePackageAsync().ConfigureAwait(false);
            return true;
        }

        await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.OpenFileFailedMessage,
            Strings.OpenFileFailedCaption,
            MessageBoxIcon.Error);
        return false;
    }

    /// <summary>
    ///     Loads the package and builds its tree
    /// </summary>
    /// <param name="path">The package to load</param>
    /// <returns>The built tree, or null when the package could not be loaded</returns>
    private async Task<ObservableCollection<PackageEntry>?> LoadAndBuildTreeAsync(string path)
    {
        var success = await _packageService.LoadPackageAsync(path);
        return success ? await BuildPackageTreeAsync().ConfigureAwait(false) : null;
    }

    /// <summary>
    ///     Builds the tree from the loaded package's paths, optionally narrowed by the tree search
    ///     query. Runs on the caller's thread: every caller does this on a worker and publishes the
    ///     result from the UI thread.
    /// </summary>
    /// <param name="searchQuery">The tree search box query.</param>
    /// <returns>The tree, or null when no package is loaded.</returns>
    private async Task<ObservableCollection<PackageEntry>?> BuildPackageTreeAsync(string? searchQuery = null)
    {
        if (!_packageService.IsLoaded) return null;

        var files = await _packageService.Search(searchQuery).ConfigureAwait(false);
        if (files is null) return null;
        var paths = files.Select(f => f.Name);
        return PackageEntry.BuildTree(paths);
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
