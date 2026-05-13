using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pfim;
using Serilog;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using PixelFormat = System.Windows.Media.PixelFormat;

namespace BG3PakViewer.Loader;

public static class ImageLoader
{
    public static async Task<BitmapSource?> LoadAsync(Stream stream, string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".dds" => await LoadTextureImageAsync(stream),
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tiff" or ".tif" or ".tga"
                => await LoadStandardImageAsync(stream),
            _ => throw new NotSupportedException($"Unsupported image format: {extension}")
        };
    }

    private static async Task<BitmapSource?> LoadStandardImageAsync(Stream stream)
    {
        try
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load standard image.");
            return null;
        }
    }

    private static async Task<BitmapSource?> LoadTextureImageAsync(Stream stream)
    {
        try
        {
            using var image = await Task.Run(() => Pfimage.FromStream(stream));

            var pinnedArray = GCHandle.Alloc(image.Data, GCHandleType.Pinned);
            var bitmap = BitmapSource.Create(
                image.Width,
                image.Height,
                96.0,
                96.0,
                ConvertToWpfPixelFormat(image.Format),
                null,
                pinnedArray.AddrOfPinnedObject(),
                image.DataLen,
                image.Stride);

            pinnedArray.Free();

            return bitmap;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load DDS texture.");
            return null;
        }
    }

    public static async Task<bool> ExportAsync(Stream stream, string path, string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".dds" => await ExportTextureImageAsync(stream, path),
            ".tga" => await ExportTargaImageAsync(stream, path),
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tiff" or ".tif"
                => await ExportStandardImageAsync(stream, path),
            _ => throw new NotSupportedException($"Unsupported image format: {extension}")
        };
    }
    
    private static async Task<bool> ExportStandardImageAsync(Stream stream, string path)
    {
        try
        {
            await Task.Run(() =>
            {
                using var bitmap = Image.FromStream(stream);
                bitmap.Save(path, GetImageFormatFromExtension(path));
            });

            Log.Information("Saved standard image to {Path}", path);
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to export standard image.");
            return false;
        }
    }

    private static async Task<bool> ExportTextureImageAsync(Stream stream, string path)
    {
        try
        {
            using var bitmap = await ConvertDdsToBitmapAsync(stream);
            if (bitmap == null)
                throw new InvalidOperationException("Failed to convert DDS to bitmap");

            await using var fs = File.OpenWrite(path);
            var imageFormat = GetImageFormatFromExtension(path);

            await Task.Run(() => bitmap.Save(fs, imageFormat));

            Log.Information("Saved DDS texture to {Path}", path);
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to export DDS texture.");
            return false;
        }
    }
    
    private static async Task<bool> ExportTargaImageAsync(Stream stream, string path)
    {
        try
        {
            await using var fs = File.OpenWrite(path);
            using var image = Pfimage.FromStream(stream);
            await fs.WriteAsync(image.Data);
            Log.Information("Saved TGA image to {Path}", path);
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to export TGA image.");
            return false;
        }
    } 
    

    private static async Task<Bitmap?> ConvertDdsToBitmapAsync(Stream stream)
    {
        try
        {
            using var image = await Task.Run(() => Pfimage.FromStream(stream));

            var pixelFormat = ConvertToDrawingPixelFormat(image.Format);
            var bitmap = new Bitmap(image.Width, image.Height, pixelFormat);

            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, image.Width, image.Height),
                ImageLockMode.WriteOnly,
                pixelFormat);

            Marshal.Copy(image.Data, 0, bitmapData.Scan0, image.DataLen);
            bitmap.UnlockBits(bitmapData);

            return bitmap;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to convert DDS to bitmap.");
            return null;
        }
    }

    private static ImageFormat GetImageFormatFromExtension(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => ImageFormat.Png,
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".bmp" => ImageFormat.Bmp,
            ".gif" => ImageFormat.Gif,
            ".tiff" or ".tif" => ImageFormat.Tiff,
            _ => ImageFormat.Png
        };
    }

    private static PixelFormat ConvertToWpfPixelFormat(Pfim.ImageFormat format)
    {
        return format switch
        {
            Pfim.ImageFormat.Rgb24 => PixelFormats.Bgr24,
            Pfim.ImageFormat.Rgba32 => PixelFormats.Bgra32,
            Pfim.ImageFormat.Rgb8 => PixelFormats.Gray8,
            Pfim.ImageFormat.R5g5b5a1 or Pfim.ImageFormat.R5g5b5 => PixelFormats.Bgr555,
            Pfim.ImageFormat.R5g6b5 => PixelFormats.Bgr565,
            _ => throw new NotSupportedException($"Unable to convert {format} to WPF PixelFormat")
        };
    }

    private static System.Drawing.Imaging.PixelFormat ConvertToDrawingPixelFormat(Pfim.ImageFormat format)
    {
        return format switch
        {
            Pfim.ImageFormat.Rgb24 => System.Drawing.Imaging.PixelFormat.Format24bppRgb,
            Pfim.ImageFormat.Rgba32 => System.Drawing.Imaging.PixelFormat.Format32bppArgb,
            Pfim.ImageFormat.Rgb8 => System.Drawing.Imaging.PixelFormat.Format8bppIndexed,
            Pfim.ImageFormat.R5g5b5a1 or Pfim.ImageFormat.R5g5b5 =>
                System.Drawing.Imaging.PixelFormat.Format16bppRgb555,
            Pfim.ImageFormat.R5g6b5 => System.Drawing.Imaging.PixelFormat.Format16bppRgb565,
            _ => throw new NotSupportedException($"Unable to convert {format} to Drawing PixelFormat")
        };
    }
}