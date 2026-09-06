using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

/// <summary>
///     Text based preview handler
/// </summary>
/// <param name="packageService"></param>
/// <param name="appSettings"></param>
internal abstract class TextBasedPreviewHandler(IPackageService packageService, IAppSettings appSettings)
    : IPreviewHandler
{
    /// <summary>
    ///     Can handle
    /// </summary>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    public abstract bool CanHandle(string fileExtension);

    /// <summary>
    ///     Create preview view model async
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public async Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        await using var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
        if (stream is null) return null;

        var text = await GetTextAsync(stream);

        if (string.IsNullOrEmpty(text))
            return null;

        var truncated = await Task.Run(() => TextOperations.TruncateToLines(text, appSettings.MaxPreviewLines));

        return new PlainTextPreviewViewModel { Text = truncated };
    }

    /// <summary>
    ///     Get text async
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    protected abstract Task<string?> GetTextAsync(Stream stream);
}