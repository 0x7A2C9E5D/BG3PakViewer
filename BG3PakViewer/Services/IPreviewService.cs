using BG3PakViewer.Shared.Models;

namespace BG3PakViewer.Services;

internal interface IPreviewService : IAsyncDisposable
{
    Task<object?> CreatePreviewViewModelAsync(PackageEntry node);
}