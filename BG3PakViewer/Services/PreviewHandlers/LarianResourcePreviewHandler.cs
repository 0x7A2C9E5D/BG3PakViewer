using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Loader;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

/// <summary>
///     Larian resource preview handler
/// </summary>
/// <param name="packageService"></param>
internal class LarianResourcePreviewHandler(IPackageService packageService) : IPreviewHandler
{
    /// <summary>
    ///     Can handle
    /// </summary>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsLarianResource(fileExtension);
    }

    /// <summary>
    ///     Create preview view model async
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public async Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        await using var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
        if (stream is null) return null;

        var resource = await LarianResourceLoader.LoadAsync(stream, node.FileExtension);
        if (resource == null) return null;

        // Build the tree off the UI thread so large resources don't block the UI.
        // Attributes are formatted lazily per selected node. No LSX string is
        // materialized for previewing.
        return await Task.Run(() => LarianResourcePreviewViewModel.FromResource(resource));
    }
}