using System.IO;
using Hexa.NET.DirectXTex;
using Serilog;

namespace BG3PakViewer.Loader;

public static class ImageLoader
{
    public static async Task<ScratchImage?> LoadAsync(Stream stream, string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".dds" => await LoadTextureImageAsync(stream),
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tiff" or ".tif" or ".tga"
                => await LoadStandardImageAsync(stream),
            _ => throw new NotSupportedException($"Unsupported image format: {extension}")
        };
    }

    private static async Task<ScratchImage?> LoadStandardImageAsync(Stream stream)
    {
        try
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var imageData = ms.ToArray();
            unsafe
            {
                fixed (byte* ptr = imageData)
                {
                    var image = DirectXTex.CreateScratchImage();
                    TexMetadata metadata = default;
                    var result =
                        DirectXTex.LoadFromWICMemory(ptr, (nuint)imageData.Length, WICFlags.None, ref metadata,
                            ref image, null);
                    if (result.IsSuccess)
                        return image;
                    return null;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load standard image.");
            return null;
        }
    }

    private static async Task<ScratchImage?> LoadTextureImageAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        unsafe
        {
            var image = DirectXTex.CreateScratchImage();
            fixed (byte* ptr = ms.ToArray())
            {
                var result = DirectXTex.LoadFromDDSMemory(ptr, (nuint)ms.Length, DDSFlags.None, null, null);
                return result.IsSuccess ? image : null;
            }
        }
    }

    private static async Task<bool> ExportTextureImageAsync(ScratchImage images, string path)
    {
        return await Task.Run(() =>
        {
            unsafe
            {
                var image = images.GetImage(0, 0, 0);
                var result = DirectXTex.SaveToDDSFile(image, DDSFlags.None, path);
                return Task.FromResult(result.IsSuccess);
            }
        });
    }

    private static async Task<bool> ExportStandardImageAsync(ScratchImage images, string path)
    {
        return await Task.Run(() =>
        {
            unsafe
            {
                var image = images.GetImage(0, 0, 0);
                var codec = GetWicCodecGuidFromExtension(path);
                var result = DirectXTex.SaveToWICFile(image, WICFlags.None, codec, path, null, null);
                return Task.FromResult(result.IsSuccess);
            }
        });
    }

    private static Guid GetWicCodecGuidFromExtension(string path)
    {
        var codec = GetWicCodecFromExtension(path);
        return DirectXTex.GetWICCodec(codec);
    }

    private static WICCodecs GetWicCodecFromExtension(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".png" => WICCodecs.CodecPng,
            ".jpg" or ".jpeg" => WICCodecs.CodecJpeg,
            ".bmp" => WICCodecs.CodecBmp,
            ".gif" => WICCodecs.CodecGif,
            ".tiff" or ".tif" => WICCodecs.CodecTiff,
            _ => WICCodecs.CodecPng
        };
    }
}