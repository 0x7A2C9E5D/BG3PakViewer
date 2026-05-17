using System.IO;

namespace BG3PakViewer.Services.PreviewHandlers;

public interface IMultiStreamPreviewHandler
{
    bool CanHandle(string extension);
    
    IEnumerable<string> GetRelatedFilePatterns(string primaryFilePath);

    Task<object?> CreatePreviewViewModelAsync(Dictionary<string, Stream> streams);
}