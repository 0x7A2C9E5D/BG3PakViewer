namespace BG3PakViewer.Miscellaneous;

public static class AppPaths
{
    public static readonly string AppDataDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            DebugHelper.IsDebug ? "BG3PakViewer_Debug" : "BG3PakViewer");

    public static readonly string LogDirectory = Path.Combine(AppDataDirectory, "Logs");

    public static readonly string ConfigPath = Path.Combine(AppDataDirectory, "AppSettings.json");
}