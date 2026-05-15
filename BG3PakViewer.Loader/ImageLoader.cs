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

    private static async Task<ScratchImage?> LoadTgaImageAsync(Stream stream)
    {
        try
        {
            var imageData = await ReadStreamToByteArrayAsync(stream);
            return ProcessImageWithDirectXTexAsync(imageData, (ptr, length) =>
            {
                unsafe
                {
                    var image = DirectXTex.CreateScratchImage();
                    var result = DirectXTex.LoadFromTGAMemory(ptr.ToPointer(), (nuint)length, TGAFlags.None, null,
                        ref image);
                    return result.IsSuccess ? image : null;
                }
            });
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load TGA image.");
            return null;
        }
    }

    private static async Task<ScratchImage?> LoadStandardImageAsync(Stream stream)
    {
        try
        {
            var imageData = await ReadStreamToByteArrayAsync(stream);
            return ProcessImageWithDirectXTexAsync(imageData, (ptr, length) =>
            {
                unsafe
                {
                    var image = DirectXTex.CreateScratchImage();
                    var result = DirectXTex.LoadFromWICMemory(ptr.ToPointer(), (nuint)length, WICFlags.None, null,
                        ref image, null);
                    return result.IsSuccess ? image : null;
                }
            });
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load standard image.");
            return null;
        }
    }

    private static async Task<ScratchImage?> LoadTextureImageAsync(Stream stream)
    {
        try
        {
            var imageData = await ReadStreamToByteArrayAsync(stream);
            return ProcessImageWithDirectXTexAsync(imageData, (ptr, length) =>
            {
                unsafe
                {
                    var rawPtr = ptr.ToPointer();
                    var images = DirectXTex.CreateScratchImage();
                    var result = DirectXTex.LoadFromDDSMemory(rawPtr, (nuint)length, DDSFlags.None, null, ref images);
                    return result.IsSuccess ? images : null;
                }
            });
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load DDS texture.");
            return null;
        }
    }

    private static async Task<byte[]> ReadStreamToByteArrayAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    private static ScratchImage? ProcessImageWithDirectXTexAsync(byte[] imageData,
        Func<IntPtr, int, ScratchImage?> processor)
    {
        unsafe
        {
            fixed (byte* ptr = imageData)
            {
                return processor((IntPtr)ptr, imageData.Length);
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
                return result.IsSuccess;
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
                return result.IsSuccess;
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