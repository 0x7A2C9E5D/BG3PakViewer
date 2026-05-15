using System.IO;
using BG3PakViewer.Locales;
using BG3PakViewer.Services.ExportStrategies;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using LSLib.LS;
using Serilog;

namespace BG3PakViewer.Services;

internal class ExportService(
    IPackageService packageService,
    IEnumerable<IExportStrategy> strategies)
    : IExportService
{
    private readonly RawFileExportStrategy _defaultStrategy = new();
    private readonly Dictionary<string, IExportStrategy> _exportStrategies = BuildStrategyDictionary(strategies);

    public FileFilter[] GetExportFilters(string fileName, string fileExtension)
    {
        if (!_exportStrategies.TryGetValue(fileExtension, out var strategy))
            return _defaultStrategy.Filters;
        if (strategy is not ImageExportStrategy imageStrategy) return strategy.Filters;
        if (FileExtensions.IsLowTexTexture(fileName))
            return [new FileFilter(Strings.DDSImage, ".dds")];
        var filters = imageStrategy.GetOrderedFilters(fileExtension);
        return !fileExtension.Equals(".dds", StringComparison.OrdinalIgnoreCase) ? 
            [.. filters.Where(f => !f.Extensions!.Any(e => e.Equals(".dds", StringComparison.OrdinalIgnoreCase)))] : filters;
    }

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
            await using var stream = file.CreateContentReader();
            var success = await strategy.ExportAsync(stream, targetPath, node.FileExtension);
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

    private IEnumerable<PackagedFileInfo> GetFolderFiles(PackageEntry folderNode)
    {
        return packageService.GetValidFiles()
            .Where(x => x.Name.StartsWith(folderNode.FullPath, StringComparison.OrdinalIgnoreCase));
    }

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

    private static async Task ExportFileToFolderAsync(PackagedFileInfo file, string folder)
    {
        var targetPath = Path.Combine(folder, file.Name);
        FileOperations.EnsureDirectoryExists(targetPath);
        try
        {
            await using var stream = file.CreateContentReader();
            if (!await FileOperations.SaveStreamToFileAsync(targetPath, stream))
                Log.Warning("Failed to export file: {Path}", file.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error exporting file to folder: {Path}", file.Name);
        }
    }

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