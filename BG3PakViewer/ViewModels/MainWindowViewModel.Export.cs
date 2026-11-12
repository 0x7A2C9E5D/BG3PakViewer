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
    ///     Is exporting
    /// </summary>
    public bool IsExporting { get; private set; }

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

        _cancellationTokenSource = new CancellationTokenSource();
        var success =
            await _exportService.ExportFolderAsync(node, storageFolder.LocalPath, _cancellationTokenSource.Token);
        if (_cancellationTokenSource.IsCancellationRequested)
            Log.Information("Folder export was cancelled");
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
        if (!await _dialogService.ShowMessageBoxConfirmAsync(this, Strings.CancelExportOperationMessage,
                Strings.CancelExportOperationCaption, MessageBoxIcon.Warning)) return false;
        await _cancellationTokenSource?.CancelAsync()!;
        IsExporting = false;
        return true;
    }
}