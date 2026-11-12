using System.IO;
using System.Text;
using BG3PakViewer.Contracts;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Extensions;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using LSLib.VirtualTextures;
using Serilog;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     Exporting every layer of the selected texture, and telling the user what was written.
/// </summary>
public partial class VirtualTexturePreviewViewModel
{
    /// <summary>
    ///     Whether there is a decoded layer that can be exported.
    /// </summary>
    private bool CanExport()
    {
        return SelectedTexture is not null && Preview is not null && !IsBusy;
    }

    /// <summary>
    ///     Exports every layer of the selected texture as one set of DDS files.
    ///     DDS is the only export format here: the raw layer data is written without re-encoding.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportAsync()
    {
        var texture = SelectedTexture;
        if (texture is null) return Task.CompletedTask;

        var meta = texture.Meta;
        var name = Path.GetFileNameWithoutExtension(texture.DisplayName);
        var layerCount = Math.Min(_loader.LayerCount, LayerNames.Length);
        return layerCount == 0
            ? Task.CompletedTask
            : ExportFilesAsync([
                .. Enumerable.Range(0, layerCount)
                    .Select(layer => ToExportFile(name, meta, layer))
            ]);
    }

    /// <summary>
    ///     Asks the user for a target folder and exports the files there.
    /// </summary>
    /// <param name="files">The files to export.</param>
    private async Task ExportFilesAsync(IReadOnlyList<PreviewExportFile> files)
    {
        var folder = await PickFolderAsync();
        if (folder is not null)
        {
            var exported = 0;
            var failures = new List<PreviewExportResult>();
            await foreach (var result in _exportService.ExportAsync(folder, files))
                if (result.Success)
                    exported++;
                else
                    failures.Add(result);

            await ReportExportResultAsync(exported, files.Count, failures, folder);
        }
        else
        {
            Log.Information("Preview export folder selection was cancelled");
        }
    }

    /// <summary>
    ///     Tells the user what was written, and which files failed together with the reason.
    /// </summary>
    /// <param name="exported">How many files were written.</param>
    /// <param name="total">How many files the export attempted.</param>
    /// <param name="failures">The files that could not be written.</param>
    /// <param name="folder">The folder the files were written to.</param>
    private async Task ReportExportResultAsync(int exported, int total, IReadOnlyList<PreviewExportResult> failures,
        string folder)
    {
        if (failures.Count == 0)
            await ReportSuccessAsync(exported, folder);
        else
            await ReportFailuresAsync(exported, total, failures, folder);
    }

    /// <summary>
    ///     Tells the user that every file was written.
    /// </summary>
    /// <param name="exported">How many files were written.</param>
    /// <param name="folder">The folder the files were written to.</param>
    private async Task ReportSuccessAsync(int exported, string folder)
    {
        Log.Information("Exported {Exported} preview files to: {Folder}", exported, folder);
        await _dialogService.ShowMessageBoxNotifyAsync(this,
            string.Format(Strings.PreviewExportCompletedMessage, exported),
            Strings.ExportCompleted, MessageBoxIcon.Success);
    }

    /// <summary>
    ///     Tells the user how many files were written and which ones failed, together with the reason.
    /// </summary>
    /// <param name="exported">How many files were written.</param>
    /// <param name="total">How many files the export attempted.</param>
    /// <param name="failures">The files that could not be written.</param>
    /// <param name="folder">The folder the files were written to.</param>
    private async Task ReportFailuresAsync(int exported, int total, IReadOnlyList<PreviewExportResult> failures,
        string folder)
    {
        Log.Warning("Exported {Exported} of {Total} preview files to: {Folder}", exported, total, folder);

        var message = new StringBuilder();
        message.AppendLine(exported == 0
            ? Strings.PreviewExportNothingMessage
            : string.Format(Strings.PreviewExportPartialMessage, exported, total));
        message.AppendLine().AppendLine(Strings.PreviewExportFailedFilesHeader);
        foreach (var failure in failures)
            message.AppendLine($"{failure.Name}: {failure.Reason}");

        await _dialogService.ShowMessageBoxNotifyAsync(this, message.ToString().TrimEnd(),
            exported == 0 ? Strings.ExportFailedCaption : Strings.PreviewExportPartiallyFailedCaption,
            MessageBoxIcon.Error);
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
    ///     Turns one layer of the selected texture into the DDS file it is exported to.
    /// </summary>
    /// <param name="textureName">The texture name, without extension.</param>
    /// <param name="meta">The texture metadata.</param>
    /// <param name="layer">The layer index.</param>
    /// <returns>The file to write.</returns>
    private PreviewExportFile ToExportFile(string textureName, FourCCTextureMeta meta, int layer)
    {
        return new PreviewExportFile($"{textureName}_{LayerNames[layer]}.dds",
            () => OpenLayerAsync(meta, layer));
    }

    /// <summary>
    ///     Opens the raw DDS data of one layer. Ownership of the stream is transferred to the
    ///     export service, which copies it to the target file and disposes it afterward.
    /// </summary>
    /// <param name="meta"></param>
    /// <param name="layer"></param>
    private async Task<Stream> OpenLayerAsync(FourCCTextureMeta meta, int layer)
    {
        return await _loader.ExtractAsync(meta, layer, null, CancellationToken.None)
               ?? throw new InvalidDataException(Strings.TitleNoData);
    }
}