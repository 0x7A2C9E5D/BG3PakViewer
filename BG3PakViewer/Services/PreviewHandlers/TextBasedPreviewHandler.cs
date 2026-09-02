using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

internal abstract class TextBasedPreviewHandler(IPackageService packageService, IAppSettings appSettings)
    : IPreviewHandler
{
    public abstract bool CanHandle(string fileExtension);

    public async Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        await using var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
        if (stream is null) return null;

        var text = await GetTextAsync(stream);

        if (string.IsNullOrEmpty(text))
            return null;

        var truncated = await TextOperations.TruncateToLinesAsync(text, appSettings.MaxPreviewLines);

        return new PlainTextPreviewViewModel { Text = truncated };
    }

    protected abstract Task<string?> GetTextAsync(Stream stream);
}