using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using LSLib.VirtualTextures;
using Serilog;

namespace BG3PakViewer.Services.ExportStrategies;

/// <summary>
///     Virtual texture export strategy
/// </summary>
/// <param name="packageService"></param>
internal class VirtualTextureExportStrategy(IPackageService packageService) : IExportStrategy
{
    /// <summary>
    ///     File filters
    /// </summary>
    public FileFilter[] Filters =>
    [
        new(Strings.VirtualTextureFile, ".gts")
    ];

    /// <summary>
    ///     Export async
    /// </summary>
    /// <param name="node"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public async Task<bool> ExportAsync(PackageEntry node, string path)
    {
        await using var stream = (await packageService.GetFileByPath(node.FullPath).ConfigureAwait(false))
            ?.CreateContentReader();
        if (stream is null) return false;
        if (!await FileOperations.SaveStreamToFileAsync(stream, path).ConfigureAwait(false))
            return false;
        await ExportVirtualTexturePagesAsync(node.FullPath, path).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    ///     Export virtual texture pages async
    /// </summary>
    /// <param name="sourcePath"></param>
    /// <param name="targetPath"></param>
    private async Task ExportVirtualTexturePagesAsync(string sourcePath, string targetPath)
    {
        var pageFileNames = GetVirtualTexturePageFileNames(targetPath);
        var sourceFolderPath = Path.GetDirectoryName(sourcePath)!;
        var targetFolderPath = Path.GetDirectoryName(targetPath)!;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };
        await Parallel.ForEachAsync(pageFileNames, parallelOptions, (pageFileName, _)
                => ExportVirtualTexturePageAsync(pageFileName, sourceFolderPath, targetFolderPath))
            .ConfigureAwait(false);
        Log.Information("Exported {Count} virtual texture page file(s) next to {TargetPath}",
            pageFileNames.Count, targetPath);
    }

    /// <summary>
    ///     Read the page file names referenced by a virtual texture tile set
    /// </summary>
    /// <param name="tileSetPath"></param>
    /// <returns></returns>
    private static List<string> GetVirtualTexturePageFileNames(string tileSetPath)
    {
        using var tileSet = new VirtualTileSet(tileSetPath);
        return [.. tileSet.PageFileInfos.Select(x => x.FileName)];
    }

    /// <summary>
    ///     Export a single virtual texture page file
    /// </summary>
    /// <param name="pageFileName"></param>
    /// <param name="sourceFolderPath"></param>
    /// <param name="targetFolderPath"></param>
    private async ValueTask ExportVirtualTexturePageAsync(string pageFileName, string sourceFolderPath,
        string targetFolderPath)
    {
        try
        {
            var sourceFilePath = Path.Combine(sourceFolderPath, pageFileName).Replace("\\", "/");
            var pageFile = await packageService.GetFileByPath(sourceFilePath).ConfigureAwait(false);
            if (pageFile == null)
            {
                Log.Warning("Page file not found: {Path}", sourceFilePath);
                return;
            }

            // The page file name comes from the tile set, so it may try to escape the target folder.
            var targetFilePath = FileOperations.ResolveInside(targetFolderPath, pageFileName);
            await using var pageStream = pageFile.CreateContentReader();
            await FileOperations.SaveStreamToFileAsync(pageStream, targetFilePath).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error exporting page file: {Path}", pageFileName);
        }
    }
}
