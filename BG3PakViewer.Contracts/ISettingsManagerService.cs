using System.Globalization;

namespace BG3PakViewer.Contracts;

public interface ISettingsManagerService
{
    IEnumerable<CultureInfo> SupportedCultures { get; }

    IAppSettings CurrentSettings { get; }
}