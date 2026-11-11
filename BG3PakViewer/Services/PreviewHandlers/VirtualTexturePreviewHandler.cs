using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Loader;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs;

namespace BG3PakViewer.Services.PreviewHandlers;

/// <summary>
///     Virtual texture preview handler
/// </summary>
/// <param name="packageService"></param>
/// <param name="dialogService">The service used by the preview to pick an export folder.</param>
/// <param name="appSettings">The settings holding the default export directory.</param>
/// <param name="exportService">The service that writes the exported layer files.</param>
// ReSharper disable once ClassNeverInstantiated.Global
internal class VirtualTexturePreviewHandler(
    IPackageService packageService,
    IPreviewExportService exportService,
    IDialogService dialogService,
    IAppSettings appSettings) : IPreviewHandler
{
    /// <summary>
    ///     Can handle
    /// </summary>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsVirtualTexture(fileExtension);
    }

    /// <summary>
    ///     Create preview view model async
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        var directory = Path.GetDirectoryName(node.FullPath)?.Replace('\\', '/') ?? string.Empty;
        var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
        if (stream is null) return null;

        IReadOnlyList<string> pageFileNames = [];
        VirtualTextureLoader? loader = null;
        try
        {
            // Parsing the tile set stays off the UI thread. The view model itself is created back
            // on the UI thread, so its property changes and command notifications happen there.
            loader = await Task.Run(() => new VirtualTextureLoader(stream, pageFileIndex =>
            {
                // ReSharper disable once AccessToModifiedClosure
                var pageFileName = pageFileNames[pageFileIndex];
                var pagePath = directory.Length == 0
                    ? pageFileName
                    : $"{directory}/{pageFileName}";
                return packageService.GetFileByPath(pagePath)?.CreateContentReader()
                       ?? throw new FileNotFoundException($"GTP page file not found in package: {pagePath}");
            }));
            pageFileNames = loader.TextureNames;
            return new VirtualTexturePreviewViewModel(loader, dialogService, appSettings, exportService);
        }
        catch
        {
            loader?.Dispose();
            throw;
        }
    }
}