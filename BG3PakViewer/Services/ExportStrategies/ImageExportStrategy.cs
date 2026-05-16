using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Serilog;

namespace BG3PakViewer.Services.ExportStrategies;

internal class ImageExportStrategy : IExportStrategy
{
    public FileFilter[] Filters =>
    [
        new(Strings.DDSImage, ".dds"),
        new(Strings.TGAImage, ".tga"),
        new(Strings.PNGImage, ".png"),
        new(Strings.BMPImage, ".bmp"),
        new(Strings.JPEGImage, [".jpg", ".jpeg"]),
        new(Strings.TIFFImage, [".tif", ".tiff"])
    ];

    public async Task<bool> ExportAsync(Stream sourceStream, string targetPath, string sourceExtension)
    {
        try
        {
            if (sourceExtension == ".dds")
            {
                await using var targetStream = File.OpenWrite(targetPath);
                await sourceStream.CopyToAsync(targetStream);
                return true;
            }

            using var image = await ImageLoader.LoadAsync(sourceStream, sourceExtension);
            return image is not null && await ImageLoader.ExportAsync(image, targetPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export image.");
            return false;
        }
    }
}