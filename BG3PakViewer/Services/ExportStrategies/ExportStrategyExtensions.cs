using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

public static class ExportStrategyExtensions
{
    public static FileFilter[] GetOrderedFilters(this IExportStrategy strategy, string sourceExtension)
    {
        if (string.IsNullOrWhiteSpace(sourceExtension))
            return strategy.Filters;

        var extension = sourceExtension.ToLowerInvariant();
        return strategy.Filters.OrderByDescending(f =>
        {
            if (f.Extensions == null) return 0;
            return f.Extensions.Any(e => e.Equals(extension, StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
        }).ToArray();
    }
}