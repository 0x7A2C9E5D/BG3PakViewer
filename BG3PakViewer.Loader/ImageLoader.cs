using System.IO;
using BG3PakViewer.Utils;
using Hexa.NET.DirectXTex;
using Pfim;
using Serilog;
using SharpDX.DXGI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;
using ImageFormat = Pfim.ImageFormat;

namespace BG3PakViewer.Loader;

/// <summary>
///     ImageLoader
/// </summary>
public static class ImageLoader
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
            return await LoadTextureImageAsync(stream, extension);
        if (FileExtensions.IsBitmapImage(extension))
            return await LoadStandardImageAsync(stream, extension);
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
        return await ExportStandardImageAsync(image, path);
    }

    /// <summary>
    ///     Exports an image to a BC7-compressed DDS texture.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    private static async Task<bool> ExportTextureAsync(Image image, string path)
    {
        try
        {
            await Task.Run(() => ExportTexture(image, path));
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export texture image");
            return false;
        }
    }

    /// <summary>
    ///     Encodes an image as a BC7-compressed DDS texture.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="path"></param>
#pragma warning disable S6640
    private static unsafe void ExportTexture(Image image, string path)
#pragma warning restore S6640
    {
        using var rgba = image.CloneAs<Rgba32>();
        var source = DirectXTex.CreateScratchImage();
        var compressed = DirectXTex.CreateScratchImage();
        try
        {
            source.Initialize2D((int)Format.R8G8B8A8_UNorm,
                (nuint)rgba.Width, (nuint)rgba.Height, 1, 1, CPFlags.None);
            rgba.CopyPixelDataTo(new Span<byte>(source.GetPixels(), (int)source.GetPixelsSize()));
            var sourceMetadata = source.GetMetadata();
            DirectXTex.Compress2(source.GetImages(), source.GetImageCount(), ref sourceMetadata,
                (int)Format.BC7_UNorm, TexCompressFlags.Bc7Quick | TexCompressFlags.Parallel, 0.5f,
                ref compressed);
            var compressedMetadata = compressed.GetMetadata();
            DirectXTex.SaveToDDSFile2(compressed.GetImages(), compressed.GetImageCount(),
                ref compressedMetadata, DDSFlags.None, path);
        }
        finally
        {
            compressed.Release();
            source.Release();
        }
    }

    /// <summary>
    ///     Loads a standard image from a stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="extension"></param>
    /// <returns></returns>
    private static async Task<Image?> LoadStandardImageAsync(Stream stream, string extension)
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
    ///     Loads a texture image from a stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="extension"></param>
    /// <returns></returns>
    private static async Task<Image?> LoadTextureImageAsync(Stream stream, string extension)
    {
        try
        {
            using var pfimImage = Pfimage.FromStream(stream);
            var pixelData = RemoveStridePadding(pfimImage);
            return await ConvertPfimToImageSharp(pfimImage, pixelData);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load texture of type {Extension}", extension);
            return null;
        }
    }

    /// <summary>
    ///     Removes stride padding from a Pfim image.
    /// </summary>
    /// <param name="image"></param>
    /// <returns></returns>
    private static byte[] RemoveStridePadding(IImage image)
    {
        var tightStride = image.Width * image.BitsPerPixel / 8;

        if (image.Stride == tightStride) return image.Data;
        var newData = new byte[image.Height * tightStride];
        for (var i = 0; i < image.Height; i++)
            Buffer.BlockCopy(image.Data, i * image.Stride, newData, i * tightStride, tightStride);
        return newData;
    }

    /// <summary>
    ///     Converts a Pfim image to an ImageSharp image.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="pixelData"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private static async Task<Image?> ConvertPfimToImageSharp(IImage image, byte[] pixelData)
    {
        return image.Format switch
        {
            ImageFormat.Rgba32 => await Task.Run(() =>
                Image.LoadPixelData<Bgra32>(pixelData, image.Width, image.Height)),
            ImageFormat.Rgb24 => await Task.Run(() =>
                Image.LoadPixelData<Bgr24>(pixelData, image.Width, image.Height)),
            ImageFormat.Rgba16 => await Task.Run(() =>
                Image.LoadPixelData<Bgra4444>(pixelData, image.Width, image.Height)),
            ImageFormat.R5g5b5 => await Task.Run(() =>
            {
                SetR5G5B5AlphaBit(pixelData);
                return Image.LoadPixelData<Bgra5551>(pixelData, image.Width, image.Height);
            }),
            ImageFormat.R5g5b5a1 => await Task.Run(() =>
                Image.LoadPixelData<Bgra5551>(pixelData, image.Width, image.Height)),
            ImageFormat.R5g6b5 => await Task.Run(() =>
                Image.LoadPixelData<Bgr565>(pixelData, image.Width, image.Height)),
            ImageFormat.Rgb8 => await Task.Run(() =>
                Image.LoadPixelData<L8>(pixelData, image.Width, image.Height)),
            ImageFormat.R16f or ImageFormat.R32f =>
                throw new NotSupportedException($"Unsupported texture format: {image.Format}"),
            _ => throw new NotSupportedException($"Unsupported texture format: {image.Format}")
        };
    }

    /// <summary>
    ///     Sets the alpha bit for R5G5B5 images.
    /// </summary>
    /// <param name="pixelData"></param>
    private static void SetR5G5B5AlphaBit(byte[] pixelData)
    {
        for (var i = 1; i < pixelData.Length; i += 2)
            pixelData[i] |= 128;
    }

    /// <summary>
    ///     Exports an image to a file.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    private static async Task<bool> ExportStandardImageAsync(Image image, string path)
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