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

        if (!IconAtlasDetector.IsIconAtlas(node.FullPath))
            return new ImagePreviewViewModel { Preview = image };

        // A file whose name looks like an atlas does not have to be one, so the grid is only used when
        // the content actually shows one. A sheet without a grid still goes through the slicer: it holds
        // a single icon, and slicing it is what keeps its padding out of the preview.
        var grid = await Task.Run(() => IconAtlasDetector.DetectGrid(image));

        // Icon atlases are previewed as a sliced icon grid. The slicing happens on the decoded image
        // itself; blank cells are dropped there, which is what keeps a half used sheet from previewing
        // as a wall of empty icons.
        var icons = await Task.Run(() => IconAtlasSlicer.Slice(image, grid));
        image.Dispose();

        return CreateIconAtlasPreview(icons);
    }

    /// <summary>
    ///     Turns the sliced icons of an atlas into a preview grid.
    /// </summary>
    /// <param name="icons">The sliced icons and the cells they were cut from.</param>
    /// <returns>The preview of the icons.</returns>
    /// <remarks>
    ///     The sliced images are only the source of the bitmaps of the preview, so they are released as
    ///     soon as each one has been converted and are never held by the preview.
    /// </remarks>
    private static IconAtlasPreviewViewModel CreateIconAtlasPreview(IReadOnlyList<IconAtlasCell> icons)
    {
        return new IconAtlasPreviewViewModel([
            .. icons.Select(cell =>
            {
                var bitmap = cell.Image.ToBitmapSource();
                cell.Image.Dispose();

                // The cell keeps the size the icon has in the atlas, which is the size of the cut bitmap.
                return new IconAtlasCellViewModel(bitmap, cell.Row, cell.Column,
                    cell.Bounds.X, cell.Bounds.Y, bitmap.PixelWidth, bitmap.PixelHeight);
            })
        ]);
    }
}