using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal class ImageExportStrategy : IExportStrategy
{
    public FileFilter[] Filters => GetBaseFilters();

    public async Task<bool> ExportAsync(Stream sourceStream, string targetPath, string sourceExtension)
    {
        var sourceExt = sourceExtension.ToLowerInvariant();
        var targetExt = Path.GetExtension(targetPath).ToLowerInvariant();

        if (IsTextureFormat(sourceExt) && IsTextureFormat(targetExt))
        {
            if (sourceExt == targetExt)
                return await FileOperations.SaveStreamToFileAsync(targetPath, sourceStream);

            return false;
        }

        if (!IsTextureFormat(sourceExt) && IsTextureFormat(targetExt))
            return false;
        var images = await ImageLoader.LoadAsync(sourceStream, sourceExtension);
        return images.HasValue && await ImageLoader.ExportAsync(images.Value, targetPath);
    }

    private static bool IsTextureFormat(string extension)
    {
        return extension.Equals(".dds", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".tga", StringComparison.OrdinalIgnoreCase);
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