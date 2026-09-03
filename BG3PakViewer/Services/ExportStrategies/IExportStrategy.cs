using BG3PakViewer.Shared.Models;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal interface IExportStrategy
{
    FileFilter[] Filters { get; }

    /// <summary>
    /// Returns the export formats available for a specific source file, allowing a strategy
    /// to restrict <see cref="Filters"/> for individual files (e.g. forbidden conversions).
    /// </summary>
    FileFilter[] GetExportFilters(string fileName, string fileExtension) => Filters;

    Task<bool> ExportAsync(PackageEntry node, string path);
}
