using BG3PakViewer.Shared.Models;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services;

internal interface IExportService
{
    Task<bool> ExportFileAsync(PackageEntry node, string targetPath);

    Task<bool> ExportFolderAsync(PackageEntry folderNode, string targetFolder, CancellationToken ct = default);

    FileFilter[] GetExportFilters(string fileName, string fileExtension);
}