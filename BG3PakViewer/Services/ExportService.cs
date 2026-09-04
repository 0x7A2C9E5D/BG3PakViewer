using System.IO;
using BG3PakViewer.Services.ExportStrategies;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using LSLib.LS;
using Serilog;

namespace BG3PakViewer.Services;

/// <summary>
///     Export service
/// </summary>
/// <param name="packageService"></param>
/// <param name="strategies"></param>
internal class ExportService(
    IPackageService packageService,
    IEnumerable<IExportStrategy> strategies)
    : IExportService
{
    private readonly IExportStrategy _defaultStrategy = new RawFileExportStrategy(packageService);
    private readonly Dictionary<string, IExportStrategy> _exportStrategies = BuildStrategyDictionary(strategies);

    /// <summary>
    ///     Get export filters
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    public FileFilter[] GetExportFilters(string fileName, string fileExtension)
    {
        var strategy = _exportStrategies.TryGetValue(fileExtension, out var s) ? s : _defaultStrategy;
        return strategy.GetExportFilters(fileName, fileExtension);
    }

    /// <summary>
    ///     Export file async
    /// </summary>
    /// <param name="node"></param>
    /// <param name="targetPath"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public async Task<bool> ExportFileAsync(PackageEntry node, string targetPath)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException(@"Target path cannot be null or empty.", nameof(targetPath));
        var file = packageService.GetFileByPath(node.FullPath);
        if (file == null)
        {
            Log.Warning("Export file not found: {Path}", node.FullPath);
            return false;
        }

        var strategy = _exportStrategies.TryGetValue(node.FileExtension, out var s) ? s : _defaultStrategy;
        Log.Information("Exporting file: {SourcePath} -> {TargetPath}", node.FullPath, targetPath);
        try
        {
            var success = await strategy.ExportAsync(node, targetPath);
            if (success)
                Log.Information("Export completed successfully: {TargetPath}", targetPath);
            else
                Log.Warning("Export failed: {TargetPath}", targetPath);

            return success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error exporting file: {Path}", node.FullPath);
            return false;
        }
    }

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
        return packageService.GetValidFiles()
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

    /// <summary>
    ///     Build strategy dictionary
    /// </summary>
    /// <param name="strategies"></param>
    /// <returns></returns>
    private static Dictionary<string, IExportStrategy> BuildStrategyDictionary(IEnumerable<IExportStrategy> strategies)
    {
        return strategies
            .SelectMany(strategy => strategy.Filters
                .Where(filter => filter.Extensions != null)
                .SelectMany(filter => filter.Extensions!)
                .Select(ext => new KeyValuePair<string, IExportStrategy>(ext, strategy)))
            .GroupBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().Value,
                StringComparer.OrdinalIgnoreCase);
    }
}