using System.IO;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Extensions;
using BG3PakViewer.Shared.Models;
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
    ///     Whether a file or folder export is writing files. Observable so the export button can
    ///     switch its label to the cancel text and stay clickable while a run is in progress.
    /// </summary>
    public bool IsExporting => _state.IsExporting;

    /// <summary>
    ///     The export command runs with something selected, or while an export is running: in that
    ///     case the button cancels the export instead of starting one.
    /// </summary>
    /// <returns>Whether the command may run.</returns>
    private bool CanExport()
    {
        return _state.CanRunExportCommand;
    }

    /// <summary>
    ///     Export async. While an export is running, the same command cancels it instead of starting
    ///     another one. Concurrent execution is allowed on purpose: the default AsyncRelayCommand
    ///     would disable the button for the whole export, and the cancel path below relies on the
    ///     command staying clickable while <see cref="IsExporting" /> is true.
    /// </summary>
    /// <param name="toSave"></param>
    [RelayCommand(AllowConcurrentExecutions = true, CanExecute = nameof(CanExport))]
    private async Task ExportAsync(object toSave)
    {
        if (_state.IsExporting)
        {
            await CancelExportFromButtonAsync();
            return;
        }

        if (toSave is not PackageEntry node || !_packageService.IsLoaded)
            return;
        // Opening another package waits on this before releasing the one the export is reading from.
        _exportCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _state.BeginTargetPick();
            string? target = null;
            try
            {
                target = await PickTargetAsync(node);
            }
            finally
            {
                if (target is null) _state.CancelTargetPick();
            }

            if (target is null) return;

            if (node.IsFolder)
            {
                _state.BeginFolderExport();
                await ExportFolderAsync(node, target);
            }
            else
            {
                _state.BeginFileExport();
                await ExportFileAsync(node, target);
            }
        }
        finally
        {
            if (_state.IsExporting) _state.EndExport();
            _exportCompletion?.TrySetResult();
            _exportCompletion = null;
        }
    }

    /// <summary>
    ///     Asks the user where the selected file or folder should be written.
    /// </summary>
    /// <param name="node">The node to export.</param>
    /// <returns>The chosen path, or <see langword="null" /> when the user cancels.</returns>
    private async Task<string?> PickTargetAsync(PackageEntry node)
    {
        if (node.IsFolder)
        {
            var storageFolder = await _dialogService.ShowOpenFolderDialogAsync(this, new OpenFolderDialogSettings
            {
                SuggestedStartLocation = GetDefaultExportLocation()
            });
            if (storageFolder != null) return storageFolder.LocalPath;
            Log.Information("Export folder selection was cancelled");
            return null;
        }

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
        if (storageFile != null) return storageFile.LocalPath;
        Log.Information("Export file selection was cancelled");
        return null;
    }

    /// <summary>
    ///     Export file async
    /// </summary>
    /// <param name="node">The file to export.</param>
    /// <param name="target">Where the file is written.</param>
    private async Task ExportFileAsync(PackageEntry node, string target)
    {
        var success = await _exportService.ExportFileAsync(node, target);
        await HandleExportResultAsync(success);
    }

    /// <summary>
    ///     Export folder async
    /// </summary>
    /// <param name="node">The folder to export.</param>
    /// <param name="target">Where the folder is written.</param>
    private async Task ExportFolderAsync(PackageEntry node, string target)
    {
        // The source belongs to this export only. Clearing it in the finally keeps the "a cancellable
        // export is running" check honest for the next attempt to open a package.
        _cancellationTokenSource = new CancellationTokenSource();
        try
        {
            var success =
                await _exportService.ExportFolderAsync(node, target, _cancellationTokenSource.Token);
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
        if (!_state.IsExporting) return true;

        // Only a folder export has a cancellation source. A single file export cannot be canceled,
        // so offering to cancel it would promise something the export cannot do.
        if (!_state.CanCancelExport)
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
        if (!_state.CanCancelExport)
        {
            await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.ExportNotCancellableMessage,
                Strings.CancelExportOperationCaption, MessageBoxIcon.Warning);
            return;
        }

        Log.Information("Export cancellation requested from the export button");
        await CancelAndWaitForExportAsync();
        // Tell the user the run actually stopped: cancellation only takes effect between files, so
        // without this notice it would look like the button did nothing.
        await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.ExportCancelledMessage,
            Strings.CancelExportOperationCaption);
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
