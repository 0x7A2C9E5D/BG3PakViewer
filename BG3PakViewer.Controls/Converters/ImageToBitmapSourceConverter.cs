using System.Globalization;
using System.Windows.Data;
using BG3PakViewer.Extensions;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Controls.Converters;

/// <summary>
///     Converts a platform-agnostic ImageSharp <see cref="Image" /> into a WPF bitmap for display.
///     This keeps <c>System.Windows.Media</c> types out of view models: they expose the decoded image
///     and the view performs the conversion at binding time.
/// </summary>
internal class ImageToBitmapSourceConverter : IValueConverter
{
    /// <summary>
    ///     Converts an <see cref="Image" /> to a <see cref="BitmapSource" />.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Image image ? image.ToBitmapSource() : null;
    }

    /// <summary>
    ///     Not supported.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}