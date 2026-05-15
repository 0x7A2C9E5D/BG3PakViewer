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
            ".tga" => await LoadTgaImageAsync(stream),
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tiff" or ".tif" or ".hdp" or ".jxr" or ".wdp" or ".ico"
                or ".heif" or ".heic"
                => await LoadStandardImageAsync(stream),
            _ => throw new NotSupportedException($"Unsupported image format: {extension}")
        };
    }
    
    public static async Task<bool> ExportAsync(ScratchImage images, string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".dds" => await ExportTextureImageAsync(images, path),
            ".tga" => await ExportTgaImageAsync(images, path),
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tiff" or ".tif" or ".hdp" or ".jxr" or ".wdp" or ".ico"
                or ".heif" or ".heic"
                => await ExportStandardImageAsync(images, path),
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
                    var result =
                        DirectXTex.LoadFromWICMemory(ptr, (nuint)imageData.Length, WICFlags.None, null,
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

    private static async Task<ScratchImage?> LoadTgaImageAsync(Stream stream)
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
                    var images = DirectXTex.CreateScratchImage();
                    var result =
                        DirectXTex.LoadFromTGAMemory(ptr, (nuint)imageData.Length, TGAFlags.None, null, ref images);
                    return result.IsSuccess ? images : null;
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
            fixed (byte* ptr = ms.ToArray())
            {
                var images = DirectXTex.CreateScratchImage();
                var result = DirectXTex.LoadFromDDSMemory(ptr, (nuint)ms.Length, DDSFlags.None, null, ref images);
                return result.IsSuccess ? images : null;
            }
        }
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

    private static async Task<bool> ExportTgaImageAsync(ScratchImage images, string path)
    {
        return await Task.Run(() =>
        {
            unsafe
            {
                var image = images.GetImage(0, 0, 0);
                var result = DirectXTex.SaveToTGAFile(image, TGAFlags.None, path, null);
                return Task.FromResult(result.IsSuccess);
            }
        });
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

    private static Guid GetWicCodecGuidFromExtension(string path)
    {
        return DirectXTex.GetWICCodec(GetWicCodecFromExtension(path));
    }

    private static WICCodecs GetWicCodecFromExtension(string path)
    {
        return WicCodecFromExtension(Path.GetExtension(path).ToLowerInvariant());
    }

    private static WICCodecs WicCodecFromExtension(string extension)
    {
        return extension switch
        {
            ".png" => WICCodecs.CodecPng,
            ".jpg" or ".jpeg" => WICCodecs.CodecJpeg,
            ".bmp" => WICCodecs.CodecBmp,
            ".gif" => WICCodecs.CodecGif,
            ".tiff" or ".tif" => WICCodecs.CodecTiff,
            ".hdp" or ".jxr" or ".wdp" => WICCodecs.CodecWmp,
            ".ico" => WICCodecs.CodecIco,
            ".heif" or ".heic" => WICCodecs.CodecHeif,
            _ => throw new ArgumentOutOfRangeException(nameof(extension))
        };
    }
}