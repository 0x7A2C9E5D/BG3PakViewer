namespace BG3PakViewer.ViewModels.States;

/// <summary>
///     The region of the main window whose state changed. Only the regions with a bound property or
///     command notify; the load and preview phases are read directly by the view model.
/// </summary>
[Flags]
internal enum MainWindowStateChange
{
    /// <summary>
    ///     The tree selection changed.
    /// </summary>
    Selection = 1 << 0,

    /// <summary>
    ///     The export region changed.
    /// </summary>
    Export = 1 << 1
}
