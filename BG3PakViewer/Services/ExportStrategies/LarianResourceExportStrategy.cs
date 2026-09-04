using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

/// <summary>
///     Larian resource export strategy
/// </summary>
/// <param name="packageService"></param>
internal class LarianResourceExportStrategy(IPackageService packageService) : IExportStrategy
{
    /// <summary>
    ///     File filters
    /// </summary>
    public FileFilter[] Filters =>
    [
        new(Strings.LarianResourceFile, ".lsx"),
        new(Strings.LarianResourceFile, ".lsj"),
        new(Strings.LarianResourceFile, ".lsf"),
        new(Strings.LarianResourceFile, ".lsfx"),
        new(Strings.LarianResourceFile, ".lsb"),
        new(Strings.LarianResourceFile, ".lsbs")
    ];

    /// <summary>
    ///     Get export filters
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
        var resource = await LarianResourceLoader.LoadAsync(stream, node.FileExtension);
        return resource != null && await LarianResourceLoader.ExportAsync(resource, path);
    }
}