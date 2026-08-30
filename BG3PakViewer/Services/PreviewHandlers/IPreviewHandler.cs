using System.IO;
using BG3PakViewer.Shared.Models;

namespace BG3PakViewer.Services.PreviewHandlers;

internal interface IPreviewHandler
{
    bool CanHandle(string fileExtension);

    Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension);
    
    Task<object?> CreatePreviewViewModelAsync(PackageEntry node) => Task.FromResult<object?>(null);
}