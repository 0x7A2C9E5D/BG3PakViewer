using System.Globalization;

namespace BG3PakViewer.Locales;

/// <summary>
///     ICultureResolver
/// </summary>
public interface ICultureResolver
{
    /// <summary>
    ///     Gets the supported cultures.
    /// </summary>
    IReadOnlyList<CultureInfo> SupportedCultures { get; }

    /// <summary>
    ///     Resolves the supported culture.
    /// </summary>
    /// <returns></returns>
    CultureInfo ResolveSupportedCulture();
}