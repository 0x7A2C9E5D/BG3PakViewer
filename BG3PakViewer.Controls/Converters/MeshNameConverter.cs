using System.Globalization;
using System.Windows.Data;
using LSLib.Granny.Model;

namespace BG3PakViewer.Controls.Converters;

/// <summary>
///     Converts a <see cref="Mesh" /> to its display name. LSLib exposes the name as a field, which
///     WPF bindings cannot read directly, so a converter is needed for the combo box items.
/// </summary>
internal class MeshNameConverter : IValueConverter
{
    /// <summary>
    ///     Converts a mesh to its name.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Mesh mesh ? mesh.Name : string.Empty;
    }

    /// <summary>
    ///     Not supported; the combo box is read-only.
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
