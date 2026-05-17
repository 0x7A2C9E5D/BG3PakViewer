using System.IO;
using Pfim;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;
using ImageFormat = Pfim.ImageFormat;

namespace BG3PakViewer.Loader;

public static class ImageLoader
{
    public static async Task<Image?> LoadAsync(Stream stream, string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".dds" => await LoadTextureImageAsync(stream),
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tiff" or ".tif" or ".tga"
                => await LoadStandardImageAsync(stream),
            _ => throw new NotSupportedException($"Unsupported image format: {extension}")
        };
    }

    public static async Task<bool> ExportAsync(Image images, string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tiff" or ".tif" or ".tga"
                => await ExportStandardImageAsync(images, path),
            _ => throw new NotSupportedException($"Unsupported image format: {extension}")
        };
    }

    private static async Task<Image?> LoadStandardImageAsync(Stream stream)
    {
        try
        {
            return await Image.LoadAsync(stream);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load standard image.");
            return null;
        }
    }

    private static async Task<Image?> LoadTextureImageAsync(Stream stream)
    {
        try
        {
            using var pfimImage = Pfimage.FromStream(stream);
            var pixelData = RemoveStridePadding(pfimImage);
            return await ConvertPfimToImageSharp(pfimImage, pixelData);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load texture image.");
            return null;
        }
    }

    private static byte[] RemoveStridePadding(IImage image)
    {
        var tightStride = image.Width * image.BitsPerPixel / 8;

        if (image.Stride == tightStride) return image.Data;
        var newData = new byte[image.Height * tightStride];
        for (var i = 0; i < image.Height; i++)
            Buffer.BlockCopy(image.Data, i * image.Stride, newData, i * tightStride, tightStride);
        return newData;
    }

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

    private static void SetR5G5B5AlphaBit(byte[] pixelData)
    {
        for (var i = 1; i < pixelData.Length; i += 2)
            pixelData[i] |= 128;
    }

    private static async Task<bool> ExportStandardImageAsync(Image image, string path)
    {
        return await Task.Run(async () =>
        {
            try
            {
                await image.SaveAsync(path);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to export standard image.");
                return false;
            }
        });
    }
}