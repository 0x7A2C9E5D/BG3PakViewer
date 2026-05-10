using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Loader;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

public class ImagePreviewHandler : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsBitmapImage(fileExtension)
               || FileExtensions.IsTextureFormat(fileExtension);
    }

    public async Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension)
    {
        var image = await ImageLoader.LoadAsync(stream, fileExtension);
        return image != null ? new ImageFileViewModel { Data = image } : null;
    }
}