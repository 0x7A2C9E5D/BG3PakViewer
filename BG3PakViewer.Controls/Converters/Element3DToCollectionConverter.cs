using System.Globalization;
using System.Windows.Data;
using HelixToolkit.Wpf.SharpDX;

namespace BG3PakViewer.Controls.Converters;

/// <summary>
///     Wraps a single <see cref="Element3D" /> into an observable collection for ItemsModel3D.
/// </summary>
internal class Element3DToCollectionConverter : IValueConverter
{
    /// <summary>
    ///     Converts a single element into a collection containing that element.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Element3D element3D)
            return new ObservableElement3DCollection { element3D };
        return Binding.DoNothing;
    }

    /// <summary>
    ///     Not supported; the viewport model is read-only from the view.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
