using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hexa.NET.DirectXTex;

namespace BG3PakViewer.Extensions;

public static class ScratchImageExtensions
{
    public static unsafe BitmapSource ToBitmapSource(this ScratchImage images)
    {
        var decompressedImage = DecompressIfNeeded(images.GetImage(0, 0, 0));
        var bgra32Format = EnsureBgra32Format(decompressedImage);
        var width = (int)bgra32Format->Width;
        var height = (int)bgra32Format->Height;
        var pixels = bgra32Format->Pixels;
        var rowPitch = (int)bgra32Format->RowPitch;
        var dataLen = rowPitch * height;
        var bitmap = BitmapSource.Create(width, height,
            96.0, 96.0, PixelFormats.Bgra32, null,
            PointerToByteArray(pixels, dataLen), rowPitch);
        bitmap.Freeze();
        return bitmap;
    }

    private static unsafe Image* EnsureBgra32Format(Image* image)
    {
        if (image->Format == 87) return image;
        var converted = DirectXTex.CreateScratchImage();
        var result = DirectXTex.Convert(image, 87, TexFilterFlags.Default, 0.0f, ref converted);
        image = result.IsSuccess ? converted.GetImage(0, 0, 0) : throw new InvalidOperationException();
        return image;
    }

    private static unsafe Image* DecompressIfNeeded(Image* image)
    {
        if (!DirectXTex.IsCompressed(image->Format)) return image;
        var unCompressedImages = DirectXTex.CreateScratchImage();
        var result = DirectXTex.Decompress(image, 87, ref unCompressedImages);
        return result.IsFailure ? throw new InvalidOperationException() : unCompressedImages.GetImage(0, 0, 0);
    }

    private static unsafe byte[] PointerToByteArray(byte* pointer, int length)
    {
        var result = new byte[length];
        fixed (byte* ptr = result)
        {
            Buffer.MemoryCopy(pointer, ptr,
                length, length);
        }

        return result;
    }
}