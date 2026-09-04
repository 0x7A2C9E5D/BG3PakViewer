using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

/// <summary>
///     Image export strategy
/// </summary>
/// <param name="packageService"></param>
internal class ImageExportStrategy(IPackageService packageService) : IExportStrategy
{
    /// <summary>
    ///     Image export strategy
    /// </summary>
    public FileFilter[] Filters =>
    [
        new(Strings.DDSImage, ".dds"),
        new(Strings.TGAImage, ".tga"),
        new(Strings.PNGImage, ".png"),
        new(Strings.BMPImage, ".bmp"),
        new(Strings.JPEGImage, [".jpg", ".jpeg"]),
        new(Strings.TIFFImage, [".tif", ".tiff"])
    ];

    /// <summary>
    ///     Get export filters
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    public FileFilter[] GetExportFilters(string fileName, string fileExtension)
    {
        return
        [
            .. Filters.Where(f =>
                f.Extensions!.Any(ext => GetOperation(fileName, fileExtension, ext) != ExportOperation.Forbidden))
        ];
    }

    /// <summary>
    ///     Export async
    /// </summary>
    /// <param name="node"></param>
    /// <param name="path"></param>
    /// <returns></returns>
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

    /// <summary>
    ///     Get operation
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="sourceExtension"></param>
    /// <param name="targetExtension"></param>
    /// <returns></returns>
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