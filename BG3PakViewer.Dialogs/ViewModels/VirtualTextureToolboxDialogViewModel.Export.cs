using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using BG3PakViewer.Contracts;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Extensions;
using BG3PakViewer.VirtualTextures.Models;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Serilog;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     Exporting the selected textures, one folder per tile set, and telling the user what was written.
/// </summary>
public partial class VirtualTextureToolboxDialogViewModel
{
    private CancellationTokenSource? _exportCancellation;

    /// <summary>
    ///     The completion message template, parsed once per dialog instead of per export.
    /// </summary>
    private readonly CompositeFormat _completedMessageFormat =
        CompositeFormat.Parse(Strings.PreviewExportCompletedMessage);

    /// <summary>
    ///     The partial completion message template, parsed once per dialog instead of per export.
    /// </summary>
    private readonly CompositeFormat _partialMessageFormat =
        CompositeFormat.Parse(Strings.PreviewExportPartialMessage);

    /// <summary>
    ///     Whether there is a selected texture to export.
    /// </summary>
    /// <returns></returns>
    private bool CanExportSelected()
    {
        return !IsIndexing && !IsExporting && SelectedCount > 0;
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
    /// <param name="rows">The textures to export, each of their layers as one DDS file.</param>
    private async Task ExportAsync(IReadOnlyList<VirtualTextureToolboxTextureViewModel> rows)
    {
        if (rows.Count == 0) return;

        var folder = await PickFolderAsync();
        if (folder is null)
        {
            Log.Information("Virtual texture toolbox export folder selection was cancelled");
            return;
        }

        _exportCancellation?.Dispose();
        _exportCancellation = new CancellationTokenSource();
        var cancellationToken = _exportCancellation.Token;
        var files = BuildFiles(folder, rows, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        IsExporting = true;
        Progress = 0;

        try
        {
            var exported = 0;
            var failures = new List<PreviewExportResult>();
            await foreach (var result in _exportService.ExportAsync(folder, files, cancellationToken))
            {
                exported++;
                if (!result.Success) failures.Add(result);
                Progress = files.Count == 0 ? 0 : exported * 100.0 / files.Count;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Log.Information(
                    "Virtual texture toolbox export cancelled: {TextureCount} texture(s), {Files}/{Total} files in {Elapsed} ms",
                    rows.Count, exported, files.Count, stopwatch.ElapsedMilliseconds);
                return;
            }

            Log.Information(
                "Virtual texture toolbox export finished: {TextureCount} texture(s), {Files}/{Total} files in {Elapsed} ms",
                rows.Count, exported, files.Count, stopwatch.ElapsedMilliseconds);
            await ReportExportResultAsync(exported, files.Count, failures);
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
    ///     Turns the textures into the files they are exported as, one sub folder per tile set.
    /// </summary>
    /// <param name="folder">The folder the sub folders are created in.</param>
    /// <param name="rows">The textures to export.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>The files to write, grouped by tile set.</returns>
    private List<PreviewExportFile> BuildFiles(string folder, IReadOnlyList<VirtualTextureToolboxTextureViewModel> rows,
        CancellationToken cancellationToken)
    {
        var files = new List<PreviewExportFile>();
        foreach (var group in rows.GroupBy(row => row.Entry.GtsPath))
        {
            var folderName = Path.GetFileNameWithoutExtension(group.Key);
            Directory.CreateDirectory(Path.Combine(folder, folderName));
            foreach (var row in group)
            {
                var layerCount = Math.Min(row.Entry.LayerCount, VirtualTextureLayers.Names.Count);
                for (var layer = 0; layer < layerCount; layer++)
                {
                    var layerIndex = layer;
                    var fileName = Path.Combine(folderName,
                        $"{row.DisplayName}_{VirtualTextureLayers.Names[layerIndex]}.dds");
                    files.Add(new PreviewExportFile(fileName,
                        () => OpenLayerAsync(row.Entry, layerIndex, cancellationToken)));
                }
            }
        }

        return files;
    }

    /// <summary>
    ///     Opens the raw DDS data of one layer. Ownership of the stream is transferred to the export
    ///     service, which copies it to the target file and disposes it afterward.
    /// </summary>
    /// <param name="entry">The texture to extract.</param>
    /// <param name="layer">The layer index.</param>
    /// <param name="cancellationToken">Cancels the extraction.</param>
    /// <returns>The layer data.</returns>
    private async Task<Stream> OpenLayerAsync(VirtualTextureEntry entry, int layer,
        CancellationToken cancellationToken)
    {
        return await _catalog.ExtractLayerAsync(entry, layer, null, cancellationToken)
               ?? throw new InvalidDataException(Strings.TitleNoData);
    }

    /// <summary>
    ///     Asks the user where the files should be written.
    /// </summary>
    /// <returns>The chosen folder, or <see langword="null" /> when the user cancels.</returns>
    private async Task<string?> PickFolderAsync()
    {
        var storageFolder = await _dialogService.ShowOpenFolderDialogAsync(null, new OpenFolderDialogSettings
        {
            SuggestedStartLocation = Directory.Exists(_appSettings.DefaultExportDirectory)
                ? new DesktopDialogStorageFolder(_appSettings.DefaultExportDirectory)
                : null
        });
        return storageFolder?.LocalPath;
    }

    /// <summary>
    ///     Tells the user what was written, and which files failed together with the reason.
    /// </summary>
    /// <param name="exported">How many files were written.</param>
    /// <param name="total">How many files the export attempted.</param>
    /// <param name="failures">The files that could not be written.</param>
    private async Task ReportExportResultAsync(int exported, int total, List<PreviewExportResult> failures)
    {
        if (failures.Count == 0)
        {
            await _dialogService.ShowMessageBoxNotifyAsync(this,
                string.Format(CultureInfo.CurrentCulture, _completedMessageFormat, exported),
                Strings.ExportCompleted, MessageBoxIcon.Success);
            return;
        }

        var message = new StringBuilder();
        message.AppendLine(exported == 0
            ? Strings.PreviewExportNothingMessage
            : string.Format(CultureInfo.CurrentCulture, _partialMessageFormat, exported, total));
        message.AppendLine().AppendLine(Strings.PreviewExportFailedFilesHeader);
        foreach (var failure in failures)
            message.AppendLine(CultureInfo.CurrentCulture, $"{failure.Name}: {failure.Reason}");

        await _dialogService.ShowMessageBoxNotifyAsync(this, message.ToString().TrimEnd(),
            exported == 0 ? Strings.ExportFailedCaption : Strings.PreviewExportPartiallyFailedCaption,
            MessageBoxIcon.Error);
    }
}