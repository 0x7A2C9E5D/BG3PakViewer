using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using LSLib.LS.Story;

namespace BG3PakViewer.Services.PreviewHandlers;

public class StoryScriptsPreviewHandler(IPackageService packageService, IAppSettings appSettings) : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsOsirisScript(fileExtension);
    }

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