using System.Globalization;

namespace BG3PakViewer.Locales;

public interface ICultureResolver
{
    public IReadOnlyList<CultureInfo> SupportedCultures { get; }

    CultureInfo ResolveSupportedCulture();
}