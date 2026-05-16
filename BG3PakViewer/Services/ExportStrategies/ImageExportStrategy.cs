using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

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
        new(Strings.TIFFImage, [".tif", ".tiff"]),
        new(Strings.GIFImage, ".gif")
    ];

    public async Task<bool> ExportAsync(Stream sourceStream, string targetPath, string sourceExtension)
    {
        var image = await ImageLoader.LoadAsync(sourceStream, sourceExtension);
        return image is not null && await ImageLoader.ExportAsync(image, targetPath);
    }
}