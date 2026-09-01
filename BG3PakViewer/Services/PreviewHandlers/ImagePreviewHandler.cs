using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Extensions;
using BG3PakViewer.Loader;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

internal class ImagePreviewHandler(IPackageService packageService) : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsBitmapImage(fileExtension)
               || FileExtensions.IsTextureFormat(fileExtension);
    }

    public async Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        await using var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
        if (stream is null) return null;

        using var image = await ImageLoader.LoadAsync(stream, node.FileExtension);
        return image is null ? null : new ImagePreviewViewModel { Data = image.ToBitmapSource() };
    }
}