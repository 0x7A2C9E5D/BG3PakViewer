using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using BG3PakViewer.Contracts;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Extensions;
using HanumanInstitute.MvvmDialogs;
using Microsoft.Win32;
using Serilog;

namespace BG3PakViewer.Services;

/// <summary>
///     Exports the icons of an atlas preview as PNG files.
/// </summary>
/// <param name="appSettings"></param>
/// <param name="dialogService"></param>
internal sealed class IconExportService(IAppSettings appSettings, IDialogService dialogService)
    : IIconExportService
{
    /// <summary>
    ///     Asks the user for a target folder and writes the given icons there as PNG files.
    /// </summary>
    /// <param name="items">The icons to export, with the names of the files they are written to.</param>
    public async Task ExportAsync(IReadOnlyList<IconExportItem> items)
    {
        if (items.Count == 0) return;

        var dialog = new OpenFolderDialog
        {
            InitialDirectory = Directory.Exists(appSettings.DefaultExportDirectory)
                ? appSettings.DefaultExportDirectory
                : string.Empty
        };
        if (dialog.ShowDialog() != true)
        {
            Log.Information("Icon export folder selection was cancelled");
            return;
        }

        var exported = 0;
        foreach (var item in items)
        {
            var path = Path.Combine(dialog.FolderName, item.FileName);
            try
            {
                // The bitmaps are frozen, so they can be encoded off the UI thread while a large
                // atlas is being exported.
                var data = await Task.Run(() => EncodePng(item.Image));
                await File.WriteAllBytesAsync(path, data);
                exported++;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to export icon to: {Path}", path);
            }
        }

        Log.Information("Exported {Exported} of {Total} icons", exported, items.Count);
        if (Application.Current.MainWindow?.DataContext is not INotifyPropertyChanged owner) return;
        if (exported == items.Count)
            await dialogService.ShowMessageBoxNotifyAsync(owner, Strings.ExportCompleted, Strings.ExportCompleted,
                MessageBoxIcon.Success);
        else
            await dialogService.ShowMessageBoxNotifyAsync(owner, Strings.ExportFailedMessage,
                Strings.ExportFailedCaption, MessageBoxIcon.Error);
    }

    /// <summary>
    ///     Encodes an icon as PNG.
    /// </summary>
    /// <param name="image">The frozen bitmap of the icon.</param>
    /// <returns>The encoded PNG data.</returns>
    private static byte[] EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}