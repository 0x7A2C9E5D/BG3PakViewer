using System.Globalization;
using System.Runtime.InteropServices;
using BG3PakViewer.Contracts;
using BG3PakViewer.Locales;
using BG3PakViewer.Miscellaneous;
using Serilog;

namespace BG3PakViewer.Services;

/// <summary>
///     Application diagnostics
/// </summary>
/// <param name="appSettings"></param>
/// <param name="cultureResolver"></param>
internal class AppDiagnostics(
    IAppSettings appSettings,
    ICultureResolver cultureResolver)
    : IAppDiagnostics
{
    /// <summary>
    ///     Log startup info
    /// </summary>
    public void LogStartupInfo()
    {
        LogApplicationInfo();
        LogEnvironmentInfo();
        LogLocalizationInfo();
    }

    /// <summary>
    ///     Log application info
    /// </summary>
    private static void LogApplicationInfo()
    {
        Log.Information("Application started.");
        Log.Information("Application Version: {Version}", ThisAssembly.AssemblyFileVersion);
        Log.Information("Is Debug: {IsDebug}", !BuildInformation.IsReleaseBuild);
    }

    /// <summary>
    ///     Log environment info
    /// </summary>
    private static void LogEnvironmentInfo()
    {
        Log.Information("OS Version: {Version}",
            $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        Log.Information(".Net Runtime: {Runtime}", RuntimeInformation.FrameworkDescription);
        Log.Information("Current Directory: {Directory}", Environment.CurrentDirectory);
        Log.Information("AppData Folder: {Folder}", AppPaths.AppDataDirectory);
    }

    /// <summary>
    ///     Log localization info
    /// </summary>
    private void LogLocalizationInfo()
    {
        var supportedCultures = cultureResolver.SupportedCultures;
        Log.Information("Installed Language Packs: {Languages}",
            string.Join(", ", supportedCultures.Select(x => x.Name)));
        // Recorded together with the resolved language so that an unexpected English UI can be
        // traced back to a system culture that has no matching language pack.
        Log.Information("System UI Culture: {Culture}", CultureInfo.InstalledUICulture.Name);
        Log.Information("Current Language: {Language}", appSettings.Language);
    }
}