using System.IO;
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
    ///     Export async
    /// </summary>
    /// <param name="node"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public async Task<bool> ExportAsync(PackageEntry node, string path)
    {
        await using var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
        if (stream is null) return false;
        if (node.FileExtension.Equals(Path.GetExtension(path), StringComparison.OrdinalIgnoreCase))
            return await FileOperations.SaveStreamToFileAsync(stream, path);
        var resource = await LocalizationLoader.LoadAsync(stream);
        return resource != null && await LocalizationLoader.ExportAsync(resource, path);
    }
}