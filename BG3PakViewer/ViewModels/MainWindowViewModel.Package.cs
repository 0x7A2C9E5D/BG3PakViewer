using System.IO;
using System.Windows;
using BG3PakViewer.Locales;
using BG3PakViewer.Messaging;
using BG3PakViewer.Shared.Extensions;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
            if (Path.GetExtension(file) is not ".pak") return;
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

            var success = await Task.Run(async () =>
            {
                PackageTree = null;
                await _packageService.CleanupAsync();
                return await LoadAndBuildTreeAsync(path);
            }, CancellationToken.None);

            if (success)
                _recentFilesService.AddOrUpdateRecentFile(path);
            else
                await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.OpenFileFailedMessage,
                    Strings.OpenFileFailedCaption,
                    MessageBoxIcon.Error);
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
}