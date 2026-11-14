using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using LSLib.LS;
using Serilog;

namespace BG3PakViewer.Services;

/// <summary>
///     Export service - folder export
/// </summary>
internal partial class ExportService
{
    /// <summary>
    ///     Export folder async
    /// </summary>
    /// <param name="folderNode"></param>
    /// <param name="targetFolder"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> ExportFolderAsync(PackageEntry folderNode, string targetFolder,
        CancellationToken ct = default)
    {
        Log.Information("Exporting folder: {FolderPath} -> {TargetFolder}", folderNode.FullPath, targetFolder);
        try
        {
            var files = GetFolderFiles(folderNode).ToList();
            if (files.Count == 0)
            {
                Log.Information("No files to export in folder: {FolderPath}", folderNode.FullPath);
                return true;
            }

            Log.Information("Found {Count} files to export", files.Count);
            var failed = await ExportFilesToFolderAsync(files, targetFolder, ct);
            if (failed > 0)
            {
                Log.Warning("Folder export finished with {Failed} of {Count} files failed: {TargetFolder}",
                    failed, files.Count, targetFolder);
                return false;
            }

            Log.Information("Folder export completed: {TargetFolder}", targetFolder);
            return true;
        }
        catch (OperationCanceledException ex)
        {
            Log.Information(ex, "Folder export was cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error exporting folder: {FolderPath}", folderNode.FullPath);
            return false;
        }
    }

    /// <summary>
    ///     Get folder files
    /// </summary>
    /// <param name="folderNode"></param>
    /// <returns></returns>
    private IEnumerable<PackagedFileInfo> GetFolderFiles(PackageEntry folderNode)
    {
        // The root node carries an empty path and covers the whole package.
        if (folderNode.FullPath.Length == 0)
            return packageService.GetValidFiles();

        // Compare whole path segments, so a sibling folder that merely starts with this name
        // (Public/SharedDev next to Public/Shared) is not exported as well.
        var folderPrefix = folderNode.FullPath + "/";
        return packageService.GetValidFiles()
            .Where(x => x.Name.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Export files to folder async
    /// </summary>
    /// <param name="files"></param>
    /// <param name="folderPath"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The number of files that could not be exported.</returns>
    private static async Task<int> ExportFilesToFolderAsync(List<PackagedFileInfo> files, string folderPath,
        CancellationToken cancellationToken)
    {
        var failures = 0;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };
        await Parallel.ForEachAsync(files, parallelOptions,
            async (file, _) =>
            {
                if (!await ExportFileToFolderAsync(file, folderPath)) Interlocked.Increment(ref failures);
            });
        return failures;
    }

    /// <summary>
    ///     Export file to folder async
    /// </summary>
    /// <param name="file"></param>
    /// <param name="folder"></param>
    /// <returns>True when the file was written completely.</returns>
    private static async Task<bool> ExportFileToFolderAsync(PackagedFileInfo file, string folder)
    {
        try
        {
            // The entry name comes from the package, so it may try to escape the chosen folder.
            var targetPath = FileOperations.ResolveInside(folder, file.Name);
            FileOperations.EnsureDirectoryExists(targetPath);
            await using var stream = file.CreateContentReader();
            if (await FileOperations.SaveStreamToFileAsync(stream, targetPath)) return true;

            Log.Warning("Failed to export file: {Path}", file.Name);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error exporting file to folder: {Path}", file.Name);
            return false;
        }
    }
}