using System.Globalization;

namespace BG3PakViewer.Locales;

/// <summary>
///     ICultureMatcher
/// </summary>
public interface ICultureMatcher
{
    /// <summary>
    ///     Matches a target culture to available cultures.
    /// </summary>
    /// <param name="targetCulture"></param>
    /// <param name="availableCultures"></param>
    /// <returns></returns>
    IEnumerable<CultureInfo> Matches(CultureInfo targetCulture, IReadOnlyList<CultureInfo> availableCultures);
}