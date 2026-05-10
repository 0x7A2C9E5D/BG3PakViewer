using System.Globalization;

namespace BG3PakViewer.Locales;

public interface ICultureMatcher
{
    IEnumerable<CultureInfo> Matches(CultureInfo targetCulture, IReadOnlyList<CultureInfo> availableCultures);
}