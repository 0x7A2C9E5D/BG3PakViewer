using BG3PakViewer.Shared.Models;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal interface IExportStrategy
{
    FileFilter[] Filters { get; }

    Task<bool> ExportAsync(PackageEntry node, string path);
}