using System.IO;

namespace BG3PakViewer.Services.PreviewHandlers;

public interface IPreviewHandler
{
    bool CanHandle(string fileExtension);

    Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension);
}