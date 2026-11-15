using System.IO;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Extensions;
using BG3PakViewer.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Serilog;

namespace BG3PakViewer.ViewModels;

/// <summary>
///     Exporting the selected file or folder, including the export state and the confirmation that
///     lets a running folder export be canceled.
/// </summary>
internal partial class MainWindowViewModel
{
    /// <summary>
    ///     Is exporting. Observable so the export button can switch its label to the cancel text and
    ///     stay clickable while a run is in progress.
    /// </summary>
    [ObservableProperty]
    public partial bool IsExporting { get; private set; }

    /// <summary>
    ///     Export async. While an export is running, the same command cancels it instead of starting
    ///     another one. Concurrent execution is allowed on purpose: the default AsyncRelayCommand
    ///     would disable the button for the whole export, and the cancel path below relies on the
    ///     command staying clickable while <see cref="IsExporting" /> is true.
    /// </summary>
    /// <param name="toSave"></param>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ExportAsync(object toSave)
    {
        if (IsExporting)
        {
            await CancelExportFromButtonAsync();
            return;
        }

        if (toSave is not PackageEntry node || !_packageService.IsLoaded)
            return;
        // Opening another package waits on this before releasing the one the export is reading from.
        _exportCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
            _exportCompletion?.TrySetResult();
            _exportCompletion = null;
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
            Log.Information("Export file selection was cancelled");
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
            Log.Information("Export folder selection was cancelled");
            return;
        }

        // The source belongs to this export only. Clearing it in the finally keeps the "a cancellable
        // export is running" check honest for the next attempt to open a package.
        _cancellationTokenSource = new CancellationTokenSource();
        try
        {
            var success =
                await _exportService.ExportFolderAsync(node, storageFolder.LocalPath, _cancellationTokenSource.Token);
            if (_cancellationTokenSource.IsCancellationRequested)
                Log.Information("Folder export was cancelled");
            else
                await HandleExportResultAsync(success);
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
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
            Log.Information("Export completed");
        }
        else
        {
            await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.ExportFailedMessage,
                Strings.ExportFailedCaption,
                MessageBoxIcon.Error);
            Log.Warning("Failed to export file");
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
    ///     Handle is exporting files
    /// </summary>
    /// <returns></returns>
    private async Task<bool> HandleIsExportingFiles()
    {
        if (!IsExporting) return true;

        // Only a folder export has a cancellation source. A single file export cannot be canceled,
        // so offering to cancel it would promise something the export cannot do.
        if (_cancellationTokenSource is null)
        {
            await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.ExportNotCancellableMessage,
                Strings.CancelExportOperationCaption, MessageBoxIcon.Warning);
            return false;
        }

        if (!await _dialogService.ShowMessageBoxConfirmAsync(this, Strings.CancelExportOperationMessage,
                Strings.CancelExportOperationCaption, MessageBoxIcon.Warning)) return false;
        // The export may have finished while the dialog was open.
        await CancelAndWaitForExportAsync();
        return true;
    }

    /// <summary>
    ///     Handles a click on the export button while an export is running: cancels a running folder
    ///     export directly, or tells the user a single file export cannot be canceled.
    /// </summary>
    private async Task CancelExportFromButtonAsync()
    {
        // Only a folder export has a cancellation source. A single file export cannot be canceled,
        // so the button cannot promise a cancellation the export cannot perform.
        if (_cancellationTokenSource is null)
        {
            await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.ExportNotCancellableMessage,
                Strings.CancelExportOperationCaption, MessageBoxIcon.Warning);
            return;
        }

        Log.Information("Export cancellation requested from the export button");
        await CancelAndWaitForExportAsync();
    }

    /// <summary>
    ///     Cancels a running export (folder exports only) and waits until it lets go of the package.
    /// </summary>
    private async Task CancelAndWaitForExportAsync()
    {
        // Only a folder export has a cancellation source. A single file export cannot be canceled and
        // is simply waited for.
        if (_cancellationTokenSource is { } cancellation) await cancellation.CancelAsync();
        // Cancelling only stops the loop between files; the file being written is still reading the
        // package that is about to be released, so wait until the export lets go of it.
        if (_exportCompletion is { } completion) await completion.Task;
    }
}
