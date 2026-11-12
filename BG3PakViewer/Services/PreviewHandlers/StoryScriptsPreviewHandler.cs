using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs;
using LSLib.LS.Story;

namespace BG3PakViewer.Services.PreviewHandlers;

/// <summary>
///     Story scripts preview handler
/// </summary>
/// <param name="packageService"></param>
/// <param name="dialogService">The service used by the preview to pick an export folder.</param>
/// <param name="appSettings">The settings holding the default export directory.</param>
/// <param name="exportService">The service that writes the exported script files.</param>
// ReSharper disable once ClassNeverInstantiated.Global
public class StoryScriptsPreviewHandler(
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
            return new StoryScriptsPreviewViewModel(appSettings, exportService, dialogService)
                { Story = story };
        });
    }
}