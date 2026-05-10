using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BG3PakViewer.Dialogs.Converters;

internal class StringToCultureInfoConverter : IValueConverter
{
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

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is CultureInfo ci ? ci.Name : DependencyProperty.UnsetValue;
    }
}