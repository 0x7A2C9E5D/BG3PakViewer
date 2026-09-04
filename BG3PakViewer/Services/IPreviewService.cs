using BG3PakViewer.Shared.Models;

namespace BG3PakViewer.Services;

/// <summary>
///     Preview service
/// </summary>
internal interface IPreviewService : IAsyncDisposable
{
    /// <summary>
    ///     Create preview view model async
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    Task<object?> CreatePreviewViewModelAsync(PackageEntry node);
}