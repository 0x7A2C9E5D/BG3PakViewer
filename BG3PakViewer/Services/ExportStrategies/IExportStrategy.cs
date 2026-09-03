using BG3PakViewer.Shared.Models;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal interface IExportStrategy
{
    FileFilter[] Filters { get; }

    /// <summary>
    ///     Returns the export formats available for a specific source file, allowing a strategy
    ///     to restrict <see cref="Filters" /> for individual files (e.g. forbidden conversions).
    /// </summary>
    FileFilter[] GetExportFilters(string fileName, string fileExtension)
    {
        return Filters;
    }

    Task<bool> ExportAsync(PackageEntry node, string path);
}

/// <summary>How a source file may be written to a target format.</summary>
internal enum ExportOperation
{
    /// <summary>Export to this format is not supported for the given source file.</summary>
    Forbidden,

    /// <summary>Copy the source bytes as-is to the target path.</summary>
    RawCopy,

    /// <summary>Decode the source and encode it into the target format.</summary>
    Convert
}