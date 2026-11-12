using System.IO;
using BG3PakViewer.Utils;
using SixLabors.ImageSharp;

namespace BG3PakViewer.Loader;

/// <summary>
///     ImageLoader
/// </summary>
/// <remarks>
///     The class only dispatches on the format here. The work itself lives in the sibling files:
///     bitmaps are decoded and encoded by ImageSharp, textures (.dds) by Pfim when reading and by
///     the BCn encoder when writing.
/// </remarks>
public static partial class ImageLoader
{
    /// <summary>
    ///     Loads an image from a stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="extension"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public static async Task<Image?> LoadAsync(Stream stream, string extension)
    {
        if (FileExtensions.IsTextureFormat(extension))
            return await LoadTextureAsync(stream, extension);
        if (FileExtensions.IsBitmapImage(extension))
            return await LoadBitmapAsync(stream, extension);
        throw new NotSupportedException($"Unsupported image format: {extension}");
    }

    /// <summary>
    ///     Exports an image to a file.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public static async Task<bool> ExportAsync(Image image, string path)
    {
        var extension = Path.GetExtension(path);
        if (FileExtensions.IsTextureFormat(extension))
            return await ExportTextureAsync(image, path);
        if (!FileExtensions.IsBitmapImage(extension))
            throw new NotSupportedException($"Unsupported image format: {extension}");
        return await ExportBitmapAsync(image, path);
    }
}