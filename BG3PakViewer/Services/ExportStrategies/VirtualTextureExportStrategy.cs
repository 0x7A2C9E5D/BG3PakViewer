using System.IO;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using LSLib.VirtualTextures;
using Serilog;

namespace BG3PakViewer.Services.ExportStrategies;

internal class VirtualTextureExportStrategy(IPackageService packageService) : IExportStrategy
{
    public FileFilter[] Filters =>
    [
        new(Strings.VirtualTextureFile, ".gts")
    ];

    public async Task<bool> ExportAsync(PackageEntry node, string path)
    {
        await using var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
        if (stream is null) return false;
        if (!await FileOperations.SaveStreamToFileAsync(stream, path))
            return false;
        await ExportVirtualTexturePagesAsync(node.FullPath, path);
        return true;
    }

    private async Task ExportVirtualTexturePagesAsync(string sourcePath, string targetPath)
    {
        using var tileSet = new VirtualTileSet(targetPath);
        var pageFileNames = tileSet.PageFileInfos.Select(x => x.FileName).ToList();
        var sourceFolderPath = Path.GetDirectoryName(sourcePath)!;
        var targetFolderPath = Path.GetDirectoryName(targetPath)!;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };
        await Parallel.ForEachAsync(pageFileNames, parallelOptions, async (pageFileName, _) =>
        {
            try
            {
                var sourceFilePath = Path.Combine(sourceFolderPath, pageFileName).Replace("\\", "/");
                var pageFile = packageService.GetFileByPath(sourceFilePath);
                if (pageFile == null)
                {
                    Log.Warning("Page file not found: {Path}", sourceFilePath);
                    return;
                }

                var targetFilePath = Path.Combine(targetFolderPath, pageFileName);
                await using var pageStream = pageFile.CreateContentReader();
                await FileOperations.SaveStreamToFileAsync(pageStream, targetFilePath);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error exporting page file: {Path}", pageFileName);
            }
        });

        Log.Information("Exported {Count} virtual texture page file(s) next to {TargetPath}",
            pageFileNames.Count, targetPath);
    }
}