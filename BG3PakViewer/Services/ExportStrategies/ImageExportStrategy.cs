using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal class ImageExportStrategy(bool isLowTex = false) : IExportStrategy
{
    public FileFilter[] Filters => GetFilters();

    public async Task<bool> ExportAsync(Stream sourceStream, string targetPath, string sourceExtension)
    {
        if (ShouldPreserveOriginalFormat(sourceExtension, targetPath))
            return await FileOperations.SaveStreamToFileAsync(targetPath, sourceStream);
        return await ImageLoader.ExportAsync(sourceStream, targetPath, sourceExtension);
    }

    private static bool ShouldPreserveOriginalFormat(string sourceExtension, string targetPath)
    {
        var targetExtension = Path.GetExtension(targetPath);
        return sourceExtension.Equals(".dds", StringComparison.OrdinalIgnoreCase)
               && targetExtension.Equals(".dds", StringComparison.OrdinalIgnoreCase);
    }

    private FileFilter[] GetFilters()
    {
        if (isLowTex) return [new FileFilter(Strings.DDSImage, ".dds")];
        return
        [
            new FileFilter(Strings.DDSImage, ".dds"),
            new FileFilter(Strings.PNGImage, ".png"),
            new FileFilter(Strings.JPEGImage, [".jpg", ".jpeg"]),
            new FileFilter(Strings.GIFImage, ".gif"),
            new FileFilter(Strings.BMPImage, ".bmp"),
            new FileFilter(Strings.TIFFImage, ".tiff")
        ];
    }
}