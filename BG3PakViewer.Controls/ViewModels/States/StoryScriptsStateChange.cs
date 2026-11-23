namespace BG3PakViewer.Controls.ViewModels.States;

/// <summary>
///     The region of the story scripts preview whose state changed.
/// </summary>
[Flags]
internal enum StoryScriptsStateChange
{
    /// <summary>
    ///     The script region changed.
    /// </summary>
    Preview = 1 << 0,

    /// <summary>
    ///     The search region changed.
    /// </summary>
    Search = 1 << 1,

    /// <summary>
    ///     The goal selection region changed.
    /// </summary>
    Selection = 1 << 2,

    /// <summary>
    ///     The export region changed.
    /// </summary>
    Export = 1 << 3
}
