using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using LSLib.LS.Story;

namespace BG3PakViewer.Services.PreviewHandlers;

/// <summary>
///     Story scripts preview handler
/// </summary>
/// <param name="packageService"></param>
/// <param name="appSettings"></param>
public class StoryScriptsPreviewHandler(IPackageService packageService, IAppSettings appSettings) : IPreviewHandler
{
    /// <summary>
    ///     Can handle
    /// </summary>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsStoryScripts(fileExtension);
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

        return await Task.Run(() =>
        {
            var reader = new StoryReader();
            var story = reader.Read(stream);
            return new StoryScriptsPreviewViewModel(appSettings) { Story = story };
        });
    }
}