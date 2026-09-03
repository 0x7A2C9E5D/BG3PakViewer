using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal class ImageExportStrategy(IPackageService packageService) : IExportStrategy
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

    public async Task<bool> ExportAsync(PackageEntry node, string path)
    {
        await using var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
        if (stream is null) return false;
        var sourceExtension = node.FileExtension;
        var targetExtension = Path.GetExtension(path);

        // DDS cannot be encoded: only DDS sources may be copied as-is; bitmap sources must not produce DDS.
        if (FileExtensions.IsTextureFormat(targetExtension))
            return FileExtensions.IsTextureFormat(sourceExtension)
                && await FileOperations.SaveStreamToFileAsync(stream, path);

        // Low-resolution thumbnail textures (_lowtex.dds) must not be converted to other formats.
        if (FileExtensions.IsLowTexTexture(node.Name))
            return false;

        // Copy bitmap sources as-is when the target format matches; otherwise convert between bitmap formats.
        if (sourceExtension.Equals(targetExtension, StringComparison.OrdinalIgnoreCase))
            return await FileOperations.SaveStreamToFileAsync(stream, path);
        using var image = await ImageLoader.LoadAsync(stream, sourceExtension);
        return image is not null && await ImageLoader.ExportAsync(image, path);
    }
}