using System.Windows.Media;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace BG3PakViewer.Extensions;

public static class ImageSharpExtensions
{
    public static BitmapSource ToBitmapSource(this Image image)
    {
        return image switch
        {
            Image<Bgra32> bgra32Image => ConvertBgra32(bgra32Image),
            Image<Bgr24> bgr24Image => ConvertBgr24(bgr24Image),
            Image<Rgba32> rgba32Image => ConvertRgba32(rgba32Image),
            Image<L8> l8Image => ConvertL8(l8Image),
            _ => ConvertToBgra32(image)
        };
    }

    private static BitmapSource ConvertBgra32(Image<Bgra32> image)
    {
        var width = image.Width;
        var height = image.Height;
        var stride = width * 4;
        var data = new byte[height * stride];

        image.CopyPixelDataTo(data);

        return BitmapSource.Create(
            width, height, 96, 96,
            PixelFormats.Bgra32, null, data, stride);
    }

    private static BitmapSource ConvertBgr24(Image<Bgr24> image)
    {
        var width = image.Width;
        var height = image.Height;
        var stride = width * 3;
        var data = new byte[height * stride];

        image.CopyPixelDataTo(data);

        return BitmapSource.Create(
            width, height, 96, 96,
            PixelFormats.Bgr24, null, data, stride);
    }

    private static BitmapSource ConvertRgba32(Image<Rgba32> image)
    {
        var width = image.Width;
        var height = image.Height;
        var stride = width * 4;
        var data = new byte[height * stride];

        image.CopyPixelDataTo(data);

        return BitmapSource.Create(
            width, height, 96, 96,
            PixelFormats.Bgra32, null, data, stride);
    }

    private static BitmapSource ConvertL8(Image<L8> image)
    {
        var width = image.Width;
        var height = image.Height;
        var data = new byte[height * width];

        image.CopyPixelDataTo(data);

        return BitmapSource.Create(
            width, height, 96, 96,
            PixelFormats.Gray8, null, data, width);
    }

    private static BitmapSource ConvertToBgra32(Image image)
    {
        using var converted = image.CloneAs<Bgra32>();
        return ConvertBgra32(converted);
    }
}