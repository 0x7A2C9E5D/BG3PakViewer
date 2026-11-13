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

    private CancellationTokenSource? _exportCancellation;

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
    private async Task ExportAsync(IReadOnlyList<VirtualTextureToolboxTextureViewModel> textures)
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
    private async Task WriteAndReportAsync(string folder, IReadOnlyList<VirtualTextureToolboxTextureViewModel> textures,
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
    ///     Builds the files to write.
    /// </summary>
    /// <param name="folder">The folder the files are written to.</param>
    /// <param name="textures">The textures to export.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>
    ///     The files to write, or <see langword="null" /> when the export was canceled while the layers were
    ///     checked or when no selected layer holds data; the reason is logged and, for the latter, shown.
    /// </returns>
    private async Task<List<PreviewExportFile>?> TryBuildFilesAsync(string folder,
        IReadOnlyList<VirtualTextureToolboxTextureViewModel> textures, CancellationToken cancellationToken)
    {
        List<PreviewExportFile> files;
        try
        {
            files = await BuildFilesAsync(folder, textures, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            Log.Information(ex, "Virtual texture toolbox export was cancelled while the layers were checked");
            return null;
        }

        if (files.Count > 0) return files;

        Log.Information("Virtual texture toolbox export has nothing to write: no selected layer holds data");
        await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.PreviewExportNothingMessage,
            Strings.ExportFailedCaption, MessageBoxIcon.Warning);
        return null;
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

    /// <summary>
    ///     Turns the textures into the files they are exported as, one sub folder per tile set.
    /// </summary>
    /// <param name="folder">The folder the sub folders are created in.</param>
    /// <param name="textures">The textures to export.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>The files to write, grouped by tile set.</returns>
    private async Task<List<PreviewExportFile>> BuildFilesAsync(string folder,
        IReadOnlyList<VirtualTextureToolboxTextureViewModel> textures, CancellationToken cancellationToken)
    {
        var files = new List<PreviewExportFile>();
        var skipped = 0;
        foreach (var group in textures.GroupBy(texture => texture.Entry.GtsPath))
        {
            var folderName = Path.GetFileNameWithoutExtension(group.Key);
            Directory.CreateDirectory(Path.Combine(folder, folderName));
            foreach (var texture in group)
            {
                var layerCount = Math.Min(texture.Entry.LayerCount, VirtualTextureLayers.Names.Count);
                for (var layer = 0; layer < layerCount; layer++)
                {
                    var layerIndex = layer;
                    if (!await _extractor.HasLayerDataAsync(texture.Entry, layerIndex, cancellationToken))
                    {
                        skipped++;
                        continue;
                    }

                    var fileName = Path.Combine(folderName,
                        $"{texture.DisplayName}_{VirtualTextureLayers.Names[layerIndex]}.dds");
                    files.Add(new PreviewExportFile(fileName,
                        () => OpenLayerAsync(texture.Entry, layerIndex, cancellationToken)));
                }
            }
        }

        if (skipped > 0)
            Log.Information("Virtual texture toolbox export skipped {Skipped} layer(s) without data", skipped);

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
        return await _extractor.ExtractLayerAsync(entry, layer, null, cancellationToken)
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