using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using BG3PakViewer.Contracts;
using BG3PakViewer.Dialogs.ViewModels.States;
using BG3PakViewer.Locales;
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
    public bool IsExporting => _state.IsExporting;

    /// <summary>
    ///     Whether the toolbox window may be closed. The window's <c>WindowCloseGuardBehavior</c> binds
    ///     this through its CanClose attached property, so a running export cannot be orphaned by
    ///     closing the window.
    /// </summary>
    public bool CanClose => _state.CanClose;

    /// <summary>
    ///     The export button label: the localized "Export selected" text normally, and the localized
    ///     "Exporting: 45%" text while a run is in progress.
    /// </summary>
    [SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'",
        Justification = "The format template is localized; caching would pin it to one language.")]
    public string ExportButtonText => IsExporting
        ? string.Format(CultureInfo.CurrentCulture, Strings.VirtualTextureExportingProgress,
            (int)Math.Round(ExportProgress))
        : Strings.ExportSelectedTextures;

    /// <summary>
    ///     Whether there is a selected texture to export, and at least one layer of it holds data.
    /// </summary>
    /// <returns></returns>
    private bool CanExportSelected()
    {
        return !IsIndexing && !IsExporting &&
               _state.Export != VirtualTextureExportState.PickingFolder &&
               SelectedCount > 0 &&
               _state.Selection == VirtualTextureSelectionState.HasData;
    }

    /// <summary>
    ///     Whether a submitted search matches at least one texture to export. With no search, the
    ///     whole list is exported through the export-all command instead.
    /// </summary>
    private bool CanExportSearchResults()
    {
        if (IsIndexing || IsExporting ||
            _state.Export == VirtualTextureExportState.PickingFolder ||
            _state.Search != VirtualTextureSearchState.Filtered ||
            Textures.Count == 0) return false;
        var filter = ItemFilter;
        return filter is not null && Textures.Any(texture => filter(texture));
    }

    /// <summary>
    ///     Whether there is at least one indexed texture to export.
    /// </summary>
    private bool CanExportAll()
    {
        return !IsIndexing && !IsExporting &&
               _state.Export != VirtualTextureExportState.PickingFolder &&
               Textures.Count > 0;
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
    ///     Exports every texture the current search matches.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportSearchResults))]
    private Task ExportSearchResultsAsync()
    {
        var filter = ItemFilter;
        return ExportAsync([.. Textures.Where(texture => filter is null || filter(texture))]);
    }

    /// <summary>
    ///     Exports every indexed texture.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportAll))]
    private Task ExportAllAsync()
    {
        return ExportAsync([.. Textures]);
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

        _state.BeginFolderPick();
        string? folder = null;
        try
        {
            folder = await PickFolderAsync();
        }
        finally
        {
            if (folder is null) _state.CancelFolderPick();
        }

        if (folder is null)
        {
            Log.Information("Virtual texture toolbox export folder selection was cancelled");
            return;
        }

        _exportCancellation?.Dispose();
        var cts = new CancellationTokenSource();
        _exportCancellation = cts;
        _state.BeginExport();
        ExportProgress = 0;

        try
        {
            await WriteAndReportAsync(folder, textures, cts.Token);
        }
        finally
        {
            _state.EndExport();
            ExportProgress = 0;
            cts.Dispose();
            if (ReferenceEquals(_exportCancellation, cts)) _exportCancellation = null;
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
            ExportProgress = exported * 100.0 / files.Count;
        }

        return (exported, failures);
    }
}
