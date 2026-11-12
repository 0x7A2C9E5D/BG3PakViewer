using System.IO;
using Serilog;
using SixLabors.ImageSharp;

namespace BG3PakViewer.Loader;

public static partial class ImageLoader
{
    /// <summary>
    ///     Loads a standard image from a stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="extension"></param>
    /// <returns></returns>
    private static async Task<Image?> LoadBitmapAsync(Stream stream, string extension)
    {
        try
        {
            return await Image.LoadAsync(stream);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load image of type {Extension}", extension);
            return null;
        }
    }

    /// <summary>
    ///     Exports an image to a file.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    private static async Task<bool> ExportBitmapAsync(Image image, string path)
    {
        try
        {
            await image.SaveAsync(path);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export standard image");
            return false;
        }
    }
}