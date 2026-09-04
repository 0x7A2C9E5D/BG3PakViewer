using BG3PakViewer.Shared.Models;

namespace BG3PakViewer.Services.PreviewHandlers;

/// <summary>
///     Preview handler
/// </summary>
internal interface IPreviewHandler
{
    /// <summary>
    ///     Can handle
    /// </summary>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    bool CanHandle(string fileExtension);

    /// <summary>
    ///     Create preview view model async
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    Task<object?> CreatePreviewViewModelAsync(PackageEntry node);
}