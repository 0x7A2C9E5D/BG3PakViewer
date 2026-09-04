using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Loader;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

/// <summary>
///     Localization preview handler
/// </summary>
/// <param name="packageService"></param>
internal class LocalizationPreviewHandler(IPackageService packageService) : IPreviewHandler
{
    /// <summary>
    ///     Can handle
    /// </summary>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsLocalizationFormat(fileExtension);
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

        var resource = await LocalizationLoader.LoadAsync(stream);
        if (resource == null) return null;

        // Build the table rows off the UI thread so large localizations don't
        // block the UI.
        return await Task.Run(() => LocalizationPreviewViewModel.FromResource(resource));
    }
}