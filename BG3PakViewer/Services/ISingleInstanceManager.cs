namespace BG3PakViewer.Services;

/// <summary>
///     Guards the process against a second running instance and lets that instance be brought forward.
/// </summary>
/// <remarks>
///     Registered as a singleton and resolved during startup, before the rest of the pipeline. The
///     IoC container is a static singleton that is never disposed, so the application releases this
///     service explicitly on exit.
/// </remarks>
internal interface ISingleInstanceManager : IDisposable
{
    /// <summary>
    ///     Whether another instance of this application is already running.
    /// </summary>
    /// <returns>True when an existing instance owns the single instance mutex.</returns>
    bool IsAnotherInstanceRunning();

    /// <summary>
    ///     Brings the window of the already running instance to the foreground.
    /// </summary>
    void ActivateExistingInstance();
}
