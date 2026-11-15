using System.Globalization;
using System.Reflection;

namespace BG3PakViewer.Locales;

/// <summary>
///     Provides culture resolution functionality for the application
///     Detects and resolves supported cultures based on available resource directories
/// </summary>
public class CultureResolver : ICultureResolver
{
    /// <summary>
    ///     Defines a contract for culture resolution functionality
    /// </summary>
    private readonly ICultureMatcher _matcher;

    /// <summary>
    ///     Initializes a new instance of the CultureResolver class.
    ///     Scans the application directory for culture-specific folders holding the satellite
    ///     assembly and builds a list of supported cultures. Analyzer folders without it are ignored.
    /// </summary>
    public CultureResolver(ICultureMatcher cultureMatcher)
    {
        _matcher = cultureMatcher; // Inject the ICultureMatcher instance 
        // Initialize the list of supported cultures with English as the default culture
        var supportedCultures = new List<CultureInfo> { new("en") };
        // Scan the application's directories for satellite resource folders. Analyzers also create
        // culture-named folders, but only the ones holding the localized assembly are real packs.
        foreach (var directory in Directory.GetDirectories(
                     Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!))
            try
            {
                var directoryInfo = new DirectoryInfo(directory);
                if (!File.Exists(Path.Combine(directoryInfo.FullName, "BG3PakViewer.Locales.resources.dll")))
                    continue;

                // An invalid culture name throws and is ignored like any other non-resource folder.
                supportedCultures.Add(new CultureInfo(directoryInfo.Name));
            }
            catch (Exception)
            {
                // Ignore directories that don't correspond to valid culture names
                // This prevents crashes when encountering non-culture directories
            }

        // Assign the final list of supported cultures to the property
        SupportedCultures = supportedCultures;
    }

    /// <summary>
    ///     Gets the collection of cultures supported by the application
    ///     This is determined by the presence of culture-specific resource directories
    /// </summary>
    public IReadOnlyList<CultureInfo> SupportedCultures { get; }

    /// <summary>
    ///     Resolves the most appropriate supported culture for the current system
    ///     Tries to match the display language of the user, and falls back through parent cultures
    ///     If no match is found, defaults to English ("en")
    /// </summary>
    /// <returns>The best matching supported culture, or English as fallback</returns>
    public CultureInfo ResolveSupportedCulture()
    {
        // The display language (Windows "Language preferences") is what the user actually reads the
        // UI in, and it can differ from the language the OS was installed in. InstalledUICulture
        // must not be used here: an English installation with a Chinese display language would
        // always fall back to English and never reach the Chinese language pack.
        var cultureInfo = CultureInfo.CurrentUICulture;
        // Check if the exact culture is supported, if so return it immediately
        var matches = _matcher.Matches(cultureInfo, SupportedCultures).ToList();
        return matches.Count != 0 ? matches[0] : CultureInfo.GetCultureInfo("en");
    }
}