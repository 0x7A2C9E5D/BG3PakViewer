using System.IO;
using System.Numerics;
using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Icons;
using BG3PakViewer.Loader;
using BG3PakViewer.Shared.Extensions;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs;
using SixLabors.ImageSharp;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Services.PreviewHandlers;

internal class ImagePreviewHandler(
    IPackageService packageService,
    IPreviewExportService exportService,
    IDialogService dialogService,
    IAppSettings appSettings)
    : IPreviewHandler
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
        var image = await ImageLoader.LoadAsync(stream, node.FileExtension);
        if (image is null) return null;
        if (!IconAtlasDetector.IsIconAtlas(node.FullPath))
            return new ImagePreviewViewModel { Preview = image };
        var name = Path.GetFileNameWithoutExtension(node.Name);
        var cells = await Task.Run(() => SliceAtlas(image));
        var viewModel = new IconAtlasPreviewViewModel(name, cells, exportService, dialogService, appSettings);
        image.Dispose();
        return viewModel;
    }

    /// <summary>
    ///     Cuts an atlas into the icons of the preview.
    /// </summary>
    /// <param name="image">The decoded atlas. It stays owned by the caller.</param>
    /// <returns>The icons of the atlas, in sheet order.</returns>
    /// <remarks>
    ///     The sliced images are only the source of the bitmaps of the preview, so they are released as
    ///     soon as each one has been converted and are never held by the preview. The conversion freezes
    ///     the bitmap, which is what makes it safe to hand over from a worker thread to the UI.
    /// </remarks>
    private static IconAtlasCellViewModel[] SliceAtlas(Image image)
    {
        var icons = IconAtlasSlicer.Slice(image, IconAtlasDetector.DetectGrid(image));
        var sheet = new Vector2(image.Width, image.Height);
        var cells = new IconAtlasCellViewModel[icons.Count];

        for (var index = 0; index < icons.Count; index++)
        {
            var cell = icons[index];
            var bitmap = cell.Image.ToBitmapSource();
            var coordinates = ToTextureCoordinates(cell.Bounds, sheet);
            cells[index] = new IconAtlasCellViewModel(bitmap, coordinates, index + 1);
            cell.Image.Dispose();
        }

        return cells;
    }

    /// <summary>
    ///     Normalizes the bounds of a cell against the size of the sheet it was cut from.
    /// </summary>
    /// <param name="bounds">The bounds of the cell, in sheet pixels.</param>
    /// <param name="sheet">The size of the sheet, in pixels.</param>
    /// <returns>The texture coordinates of the cell, in the 0..1 range.</returns>
    private static Vector4 ToTextureCoordinates(Rectangle bounds, Vector2 sheet)
    {
        return new Vector4(
            bounds.X / sheet.X,
            bounds.Y / sheet.Y,
            (bounds.X + bounds.Width) / sheet.X,
            (bounds.Y + bounds.Height) / sheet.Y);
    }
}