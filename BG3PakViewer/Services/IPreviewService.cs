using BG3PakViewer.Shared.Models;

namespace BG3PakViewer.Services;

public interface IPreviewService : IAsyncDisposable
{
    Task<object?> CreatePreviewViewModelAsync(PackageEntry node);
}