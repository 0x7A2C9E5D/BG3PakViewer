namespace BG3PakViewer.Shared.Miscellaneous;

/// <summary>
///     DebugHelper
/// </summary>
public static class DebugHelper
{
    /// <summary>
    ///     Gets a value indicating whether the application is running in debug mode.
    /// </summary>
#if DEBUG
    public static bool IsDebug => true;
#else
    public static bool IsDebug => false;
#endif
}
