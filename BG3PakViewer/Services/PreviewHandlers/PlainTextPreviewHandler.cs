using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

/// <summary>
///     Plain text preview handler
/// </summary>
/// <param name="packageService"></param>
/// <param name="appSettings"></param>
internal class PlainTextPreviewHandler(IPackageService packageService, IAppSettings appSettings)
    : IPreviewHandler
{
    /// <summary>
    ///     Can handle
    /// </summary>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsPlainText(fileExtension);
    }

    /// <summary>
    ///     Create preview view model async
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public async Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        await using var stream = (await packageService.GetFileByPath(node.FullPath).ConfigureAwait(false))
            ?.CreateContentReader();
        if (stream is null) return null;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        if (string.IsNullOrEmpty(content)) return null;

        var truncated = await Task.Run(() => TextOperations.TruncateToLines(content, appSettings.MaxPreviewLines));
        return new PlainTextPreviewViewModel(truncated);
    }
}
