using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal class ImageExportStrategy : IExportStrategy
{
    public FileFilter[] Filters => GetBaseFilters();

    public async Task<bool> ExportAsync(Stream sourceStream, string targetPath, string sourceExtension)
    {
        var images = await ImageLoader.LoadAsync(sourceStream, sourceExtension);
        return images.HasValue && await ImageLoader.ExportAsync(images.Value, targetPath);
    }

    private static FileFilter[] GetBaseFilters()
    {
        return
        [
            new FileFilter(Strings.DDSImage, ".dds"),
            new FileFilter(Strings.TGAImage, ".tga"),
            new FileFilter(Strings.PNGImage, ".png"),
            new FileFilter(Strings.JPEGImage, [".jpg", ".jpeg"]),
            new FileFilter(Strings.GIFImage, ".gif"),
            new FileFilter(Strings.BMPImage, ".bmp"),
            new FileFilter(Strings.TIFFImage, ".tiff")
        ];
    }
}