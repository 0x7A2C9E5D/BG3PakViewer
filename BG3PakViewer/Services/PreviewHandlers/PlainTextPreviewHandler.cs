using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

/// <summary>
///     Plain text preview handler
/// </summary>
/// <param name="packageService"></param>
/// <param name="appSettings"></param>
internal class PlainTextPreviewHandler(IPackageService packageService, IAppSettings appSettings)
    : TextBasedPreviewHandler(packageService, appSettings)
{
    /// <summary>
    ///     Can handle
    /// </summary>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    public override bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsPlainText(fileExtension);
    }

    /// <summary>
    ///     Get text async
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    protected override async Task<string?> GetTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, false);
        return await reader.ReadToEndAsync();
    }
}