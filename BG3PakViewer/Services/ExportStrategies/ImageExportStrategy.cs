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

    public FileFilter[] GetExportFilters(string fileName, string fileExtension)
    {
        return
        [
            .. Filters.Where(f =>
                f.Extensions!.Any(ext => GetOperation(fileName, fileExtension, ext) != ExportOperation.Forbidden))
        ];
    }

    public async Task<bool> ExportAsync(PackageEntry node, string path)
    {
        await using var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
        if (stream is null) return false;
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (GetOperation(node.Name, node.FileExtension, Path.GetExtension(path)))
        {
            case ExportOperation.RawCopy:
                return await FileOperations.SaveStreamToFileAsync(stream, path);
            case ExportOperation.Convert:
            {
                using var image = await ImageLoader.LoadAsync(stream, node.FileExtension);
                return image is not null && await ImageLoader.ExportAsync(image, path);
            }
            default:
                return false;
        }
    }

    // Single source of truth for the supported export directions, shared by GetExportFilters
    // (dialog options) and ExportAsync (runtime guard):
    //  - DDS cannot be encoded, so a .dds target can only be a raw copy of a DDS source.
    //  - Low-resolution thumbnails (_lowtex.dds) may only be exported as-is, never converted.
    //  - Bitmap sources may be copied as-is or converted to another bitmap format, never to DDS.
    private static ExportOperation GetOperation(string fileName, string sourceExtension, string targetExtension)
    {
        if (FileExtensions.IsTextureFormat(targetExtension))
            return FileExtensions.IsTextureFormat(sourceExtension)
                ? ExportOperation.RawCopy
                : ExportOperation.Forbidden;
        if (FileExtensions.IsLowTexTexture(fileName))
            return ExportOperation.Forbidden;
        return sourceExtension.Equals(targetExtension, StringComparison.OrdinalIgnoreCase)
            ? ExportOperation.RawCopy
            : ExportOperation.Convert;
    }
}