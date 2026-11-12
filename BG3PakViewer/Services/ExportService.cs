using BG3PakViewer.Services.ExportStrategies;
using BG3PakViewer.Shared.Models;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Serilog;

namespace BG3PakViewer.Services;

/// <summary>
///     Export service
/// </summary>
/// <param name="packageService"></param>
/// <param name="strategies"></param>
internal partial class ExportService(
    IPackageService packageService,
    IEnumerable<IExportStrategy> strategies)
    : IExportService
{
    private readonly IExportStrategy _defaultStrategy = new RawFileExportStrategy(packageService);
    private readonly Dictionary<string, IExportStrategy> _exportStrategies = BuildStrategyDictionary(strategies);
    private readonly IPackageService _packageService = packageService;

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
        var file = _packageService.GetFileByPath(node.FullPath);
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