using System.Diagnostics;
using BG3PakViewer.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     The export commands and the run they start: the target folder is picked in <c>ExportDialogs</c>,
///     the files are built in <c>ExportFiles</c>, then written and reported on.
/// </summary>
public partial class VirtualTextureDialogViewModel
{
    private CancellationTokenSource? _exportCancellation;

    /// <summary>
    ///     Whether the selected textures are being exported.
    /// </summary>
    [ObservableProperty]
    public partial bool IsExporting { get; private set; }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnIsExportingChanged(bool value)
    {
        ExportSelectedCommand.NotifyCanExecuteChanged();
        CancelExportCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsBusy));
    }

    /// <summary>
    ///     Whether there is a selected texture to export, and at least one layer of it holds data.
    /// </summary>
    /// <returns></returns>
    private bool CanExportSelected()
    {
        return !IsIndexing && !IsExporting && SelectedCount > 0 && _selectionHasData;
    }

    /// <summary>
    ///     Whether an export is running and can be canceled.
    /// </summary>
    /// <returns></returns>
    private bool CanCancelExport()
    {
        return IsExporting;
    }

    /// <summary>
    ///     Exports the selected textures.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportSelected))]
    private Task ExportSelectedAsync()
    {
        return ExportAsync([.. _selectedTextures]);
    }

    /// <summary>
    ///     Cancels the running export.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancelExport))]
    private void CancelExport()
    {
        _exportCancellation?.Cancel();
    }

    /// <summary>
    ///     Asks the user for a target folder and exports the given textures there.
    /// </summary>
    /// <param name="textures">The textures to export, each of their layers as one DDS file.</param>
    private async Task ExportAsync(IReadOnlyList<VirtualTextureItemViewModel> textures)
    {
        if (textures.Count == 0) return;

        var folder = await PickFolderAsync();
        if (folder is null)
        {
            Log.Information("Virtual texture toolbox export folder selection was cancelled");
            return;
        }

        _exportCancellation?.Dispose();
        _exportCancellation = new CancellationTokenSource();
        IsExporting = true;
        Progress = 0;

        try
        {
            await WriteAndReportAsync(folder, textures, _exportCancellation.Token);
        }
        finally
        {
            IsExporting = false;
            Progress = 0;
            _exportCancellation?.Dispose();
            _exportCancellation = null;
        }
    }

    /// <summary>
    ///     Writes the layers of the given textures and tells the user what was written.
    /// </summary>
    /// <param name="folder">The folder the files are written to.</param>
    /// <param name="textures">The textures to export.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    private async Task WriteAndReportAsync(string folder, IReadOnlyList<VirtualTextureItemViewModel> textures,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var files = await TryBuildFilesAsync(folder, textures, cancellationToken);
        if (files is null) return;

        var (exported, failures) = await WriteFilesAsync(folder, files, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            Log.Information(
                "Virtual texture toolbox export cancelled: {TextureCount} texture(s), {Files}/{Total} files in {Elapsed} ms",
                textures.Count, exported, files.Count, stopwatch.ElapsedMilliseconds);
            return;
        }

        Log.Information(
            "Virtual texture toolbox export finished: {TextureCount} texture(s), {Files}/{Total} files in {Elapsed} ms",
            textures.Count, exported, files.Count, stopwatch.ElapsedMilliseconds);
        await ReportExportResultAsync(exported, files.Count, failures);
    }

    /// <summary>
    ///     Writes the given files, reporting the progress, and keeps the ones that failed.
    /// </summary>
    /// <param name="folder">The folder the files are written to.</param>
    /// <param name="files">The files to write.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>How many files were processed, and the ones that failed.</returns>
    private async Task<(int Exported, List<PreviewExportResult> Failures)> WriteFilesAsync(string folder,
        List<PreviewExportFile> files, CancellationToken cancellationToken)
    {
        var exported = 0;
        var failures = new List<PreviewExportResult>();
        await foreach (var result in _exportService.ExportAsync(folder, files, cancellationToken))
        {
            exported++;
            if (!result.Success) failures.Add(result);
            Progress = exported * 100.0 / files.Count;
        }

        return (exported, failures);
    }
}
