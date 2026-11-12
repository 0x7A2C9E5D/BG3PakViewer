using System.IO;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using Pfim;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace BG3PakViewer.Loader;

public static partial class ImageLoader
{
    /// <summary>
    ///     Loads a texture image from a stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="extension"></param>
    /// <returns></returns>
    private static async Task<Image?> LoadTextureAsync(Stream stream, string extension)
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
    ///     Exports an image to a BC7-compressed DDS texture.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    private static async Task<bool> ExportTextureAsync(Image image, string path)
    {
        try
        {
            using var rgba = image.CloneAs<Rgba32>();
            var encoder = new BcEncoder
            {
                OutputOptions =
                {
                    GenerateMipMaps = false,
                    Format = CompressionFormat.Bc7,
                    Quality = CompressionQuality.BestQuality,
                    FileFormat = OutputFileFormat.Dds,
                    DdsPreferDxt10Header = true
                }
            };

            await using var stream = File.Create(path);
            await encoder.EncodeToStreamAsync(rgba, stream);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export texture image: {Path}", path);
            return false;
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
}