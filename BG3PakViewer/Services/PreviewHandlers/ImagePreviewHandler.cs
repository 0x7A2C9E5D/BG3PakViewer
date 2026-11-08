using System.Numerics;
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

        // The size of the sheet is what the UVs of the cells are normalized against, so it is captured
        // before the atlas itself is released.
        var atlasWidth = image.Width;
        var atlasHeight = image.Height;
        image.Dispose();

        return CreateIconAtlasPreview(icons, atlasWidth, atlasHeight);
    }

    /// <summary>
    ///     Turns the sliced icons of an atlas into a preview grid.
    /// </summary>
    /// <param name="icons">The sliced icons and the cells they were cut from.</param>
    /// <param name="atlasWidth">The width of the sheet the icons were cut from, in pixels.</param>
    /// <param name="atlasHeight">The height of the sheet the icons were cut from, in pixels.</param>
    /// <returns>The preview of the icons.</returns>
    /// <remarks>
    ///     The sliced images are only the source of the bitmaps of the preview, so they are released as
    ///     soon as each one has been converted and are never held by the preview.
    /// </remarks>
    private static IconAtlasPreviewViewModel CreateIconAtlasPreview(IReadOnlyList<IconAtlasCell> icons,
        int atlasWidth, int atlasHeight)
    {
        return new IconAtlasPreviewViewModel([
            .. icons.Select(cell =>
            {
                var bitmap = cell.Image.ToBitmapSource();
                cell.Image.Dispose();

                // The cell keeps the size the icon has in the atlas, which is the size of the cut bitmap.
                return new IconAtlasCellViewModel(bitmap, new Vector4(
                    cell.Bounds.X / (float)atlasWidth,
                    cell.Bounds.Y / (float)atlasHeight,
                    (cell.Bounds.X + cell.Bounds.Width) / (float)atlasWidth,
                    (cell.Bounds.Y + cell.Bounds.Height) / (float)atlasHeight));
            })
        ]);
    }
}