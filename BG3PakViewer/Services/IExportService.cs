using BG3PakViewer.Shared.Models;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services;

/// <summary>
///     Export service
/// </summary>
internal interface IExportService
{
    /// <summary>
    ///     Export file async
    /// </summary>
    /// <param name="node"></param>
    /// <param name="targetPath"></param>
    /// <returns></returns>
    Task<bool> ExportFileAsync(PackageEntry node, string targetPath);

    /// <summary>
    ///     Export folder async
    /// </summary>
    /// <param name="folderNode"></param>
    /// <param name="targetFolder"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<bool> ExportFolderAsync(PackageEntry folderNode, string targetFolder, CancellationToken ct = default);

    /// <summary>
    ///     Get export filters
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    FileFilter[] GetExportFilters(string fileName, string fileExtension);
}