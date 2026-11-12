using System.IO;
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
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public async Task<bool> ExportFolderAsync(PackageEntry folderNode, string targetFolder,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(folderNode);
        if (string.IsNullOrWhiteSpace(targetFolder))
            throw new ArgumentException(@"Target folder cannot be null or empty.", nameof(targetFolder));
        if (!folderNode.IsFolder)
        {
            Log.Warning("ExportFolderAsync called with non-folder node: {Path}", folderNode.FullPath);
            return false;
        }

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
            await ExportFilesToFolderAsync(files, targetFolder, ct);
            if (ct.IsCancellationRequested)
            {
                Log.Information("Folder export was cancelled");
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
        return _packageService.GetValidFiles()
            .Where(x => x.Name.StartsWith(folderNode.FullPath, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Export files to folder async
    /// </summary>
    /// <param name="files"></param>
    /// <param name="folderPath"></param>
    /// <param name="cancellationToken"></param>
    private static async Task ExportFilesToFolderAsync(List<PackagedFileInfo> files, string folderPath,
        CancellationToken cancellationToken)
    {
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };
        await Parallel.ForEachAsync(files, parallelOptions,
            async (file, _) => { await ExportFileToFolderAsync(file, folderPath); });
    }

    /// <summary>
    ///     Export file to folder async
    /// </summary>
    /// <param name="file"></param>
    /// <param name="folder"></param>
    private static async Task ExportFileToFolderAsync(PackagedFileInfo file, string folder)
    {
        var targetPath = Path.Combine(folder, file.Name);
        FileOperations.EnsureDirectoryExists(targetPath);
        try
        {
            await using var stream = file.CreateContentReader();
            if (!await FileOperations.SaveStreamToFileAsync(stream, targetPath))
                Log.Warning("Failed to export file: {Path}", file.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error exporting file to folder: {Path}", file.Name);
        }
    }
}