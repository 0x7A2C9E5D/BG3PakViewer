using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

/// <summary>
///     Localization export strategy
/// </summary>
/// <param name="packageService"></param>
internal class LocalizationExportStrategy(IPackageService packageService) : IExportStrategy
{
    /// <summary>
    ///     File filters
    /// </summary>
    public FileFilter[] Filters =>
    [
        new(Strings.XmlFile, ".xml"),
        new(Strings.LarianResourceFile, ".loca")
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
                f.Extensions!.Any(ext => GetOperation(fileExtension, ext) != ExportOperation.Forbidden))
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
        switch (GetOperation(node.FileExtension, Path.GetExtension(path)))
        {
            case ExportOperation.RawCopy:
                return await FileOperations.SaveStreamToFileAsync(stream, path);
            case ExportOperation.Convert:
            {
                var resource = await LocalizationLoader.LoadAsync(stream);
                return resource != null && await LocalizationLoader.ExportAsync(resource, path);
            }

            case ExportOperation.Forbidden:
            default:
                return false;
        }
    }

    /// <summary>
    ///     Get operation
    /// </summary>
    /// <param name="sourceExtension"></param>
    /// <param name="targetExtension"></param>
    /// <returns></returns>
    private static ExportOperation GetOperation(string sourceExtension, string targetExtension)
    {
        // Only a .loca file holds a localization resource that can be written as XML; an .xml
        // file cannot be turned back into the binary container.
        if (sourceExtension.Equals(".xml", StringComparison.OrdinalIgnoreCase) &&
            targetExtension.Equals(".loca", StringComparison.OrdinalIgnoreCase))
            return ExportOperation.Forbidden;
        return sourceExtension.Equals(targetExtension, StringComparison.OrdinalIgnoreCase)
            ? ExportOperation.RawCopy
            : ExportOperation.Convert;
    }
}