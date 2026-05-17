using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Utils;
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
        new(Strings.TIFFImage, [".tif", ".tiff"])
    ];

    public async Task<bool> ExportAsync(Stream stream, string path, string extension)
    {
        if (extension == ".dds")
            return await FileOperations.SaveStreamToFileAsync(path, stream);
        using var image = await ImageLoader.LoadAsync(stream, extension);
        return image is not null && await ImageLoader.ExportAsync(image, path);
    }
}