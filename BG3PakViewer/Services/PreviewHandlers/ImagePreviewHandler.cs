using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Extensions;
using BG3PakViewer.Loader;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

internal class ImagePreviewHandler : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsBitmapImage(fileExtension)
               || FileExtensions.IsTextureFormat(fileExtension);
    }

    public async Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension)
    {
        using var image = await ImageLoader.LoadAsync(stream, fileExtension);
        return image is null ? null : new ImagePreviewViewModel { Data = image.ToBitmapSource() };
    }
}