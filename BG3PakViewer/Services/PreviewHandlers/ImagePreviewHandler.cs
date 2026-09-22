using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Icons;
using BG3PakViewer.Loader;
using BG3PakViewer.Shared.Extensions;
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

        // Ownership of the image is transferred to the view model, which disposes it.
        var image = await ImageLoader.LoadAsync(stream, node.FileExtension);
        if (image is null) return null;

        if (!IconAtlasDetector.IsIconAtlas(node.FullPath)) return new ImagePreviewViewModel { Preview = image };

        // A file whose name looks like an atlas does not have to be one, so the grid is only used when
        // the content actually shows one; otherwise the plain preview is kept.
        var grid = await Task.Run(() => IconAtlasDetector.DetectGrid(image));
        if (grid is null) return new ImagePreviewViewModel { Preview = image };

        // Icon atlases are previewed as a sliced icon grid. The slicing happens in the loader, on the
        // decoded image itself; blank cells are dropped there, which is what keeps a half used sheet
        // from previewing as a wall of empty icons.
        var icons = await Task.Run(() => IconAtlasSlicer.Slice(image, grid.Value));
        image.Dispose();

        // `icons` is a list of the sliced images, which are only the source of the bitmaps, so they are
        // released as soon as they have been converted and are never held by the view model.
        // The view model only holds the bitmaps, so it is safe to dispose the icons.
        return new IconAtlasPreviewViewModel([
            .. icons.Select(icon =>
            {
                var bitmap = icon.ToBitmapSource();
                icon.Dispose();
                return bitmap;
            })
        ]);
    }
}