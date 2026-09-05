using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BG3PakViewer.Shared.Converters;

/// <summary>
///     Converter for string to culture info.
/// </summary>
public class StringToCultureInfoConverter : IValueConverter
{
    /// <summary>
    ///     Convert string to culture info.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (value is not string ci) return DependencyProperty.UnsetValue;
            return !string.IsNullOrWhiteSpace(ci) ? new CultureInfo(ci) : DependencyProperty.UnsetValue;
        }
        catch (CultureNotFoundException)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    ///     Convert culture info to string.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is CultureInfo ci ? ci.Name : DependencyProperty.UnsetValue;
    }
}