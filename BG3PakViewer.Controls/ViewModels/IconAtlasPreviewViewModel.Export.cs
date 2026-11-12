using System.IO;
using System.Windows.Media.Imaging;
using BG3PakViewer.Contracts;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     Exporting the icons of the atlas, either the selected ones or all of them.
/// </summary>
public partial class IconAtlasPreviewViewModel
{
    /// <summary>
    ///     Exports the icons the user has selected.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportSelected))]
    private Task ExportSelectedAsync()
    {
        return ExportAsync([.. Items.Where(item => item.IsSelected).Select(ToExportFile)]);
    }

    /// <summary>
    ///     Exports every icon of the atlas.
    /// </summary>
    [RelayCommand]
    private Task ExportAllAsync()
    {
        return ExportAsync([.. Items.Select(ToExportFile)]);
    }

    /// <summary>
    ///     Asks the user for a target folder and exports the files there.
    /// </summary>
    /// <param name="files">The files to export.</param>
    private async Task ExportAsync(IReadOnlyList<PreviewExportFile> files)
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
    ///     Turns an icon into the file it is exported to, named after its position in the atlas.
    /// </summary>
    /// <param name="icon">The icon to export.</param>
    /// <returns>The file to write.</returns>
    private PreviewExportFile ToExportFile(IconAtlasCellViewModel icon)
    {
        return new PreviewExportFile($"{_atlasName}_{icon.Number}.png",
            () => Task.Run(Stream () => EncodePng(icon.Image)));
    }

    /// <summary>
    ///     Encodes one sliced icon as a PNG stream.
    /// </summary>
    /// <param name="image">The icon to encode.</param>
    /// <returns>The PNG data, positioned at its start.</returns>
    private static MemoryStream EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        return stream;
    }
}