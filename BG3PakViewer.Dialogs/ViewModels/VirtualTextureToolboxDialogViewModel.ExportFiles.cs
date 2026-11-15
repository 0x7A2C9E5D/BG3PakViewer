using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Extensions;
using BG3PakViewer.VirtualTextures.Models;
using Serilog;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     Turning the selected textures into the DDS files the export writes, one sub folder per tile
///     set, with the layers that hold no data skipped before they can fail.
/// </summary>
public partial class VirtualTextureToolboxDialogViewModel
{
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
        foreach (var group in textures.GroupBy(texture => texture.Entry.path))
        {
            var folderName = Path.GetFileNameWithoutExtension(group.Key);
            Directory.CreateDirectory(Path.Combine(folder, folderName));
            foreach (var texture in group)
            {
                var layerCount = Math.Min(texture.Entry.layerCount, VirtualTextureLayers.Names.Count);
                for (var layer = 0; layer < layerCount; layer++)
                {
                    var layerIndex = layer;
                    if (!await _extractor.HasLayerAsync(texture.Entry, layerIndex, cancellationToken))
                    {
                        skipped++;
                        continue;
                    }

                    var fileName = Path.Combine(folderName,
                        $"{texture.Name}_{VirtualTextureLayers.Names[layerIndex]}.dds");
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
}
