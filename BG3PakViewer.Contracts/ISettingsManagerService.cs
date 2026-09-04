using System.Globalization;

namespace BG3PakViewer.Contracts;

/// <summary>
///     Settings manager service
/// </summary>
public interface ISettingsManagerService
{
    /// <summary>
    ///     Supported cultures
    /// </summary>
    IEnumerable<CultureInfo> SupportedCultures { get; }

    /// <summary>
    ///     Current settings
    /// </summary>
    IAppSettings CurrentSettings { get; }
}