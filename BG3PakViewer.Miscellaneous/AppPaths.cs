namespace BG3PakViewer.Miscellaneous;

/// <summary>
///     AppPaths
/// </summary>
public static class AppPaths
{
    /// <summary>
    ///     Gets the application data directory.
    /// </summary>
    public static readonly string AppDataDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            DebugHelper.IsDebug ? "BG3PakViewer_Debug" : "BG3PakViewer");

    /// <summary>
    ///     Gets the log directory.
    /// </summary>
    public static readonly string LogDirectory = Path.Combine(AppDataDirectory, "Logs");

    /// <summary>
    ///     Gets the config path.
    /// </summary>
    public static readonly string ConfigPath = Path.Combine(AppDataDirectory, "AppSettings.json");
}