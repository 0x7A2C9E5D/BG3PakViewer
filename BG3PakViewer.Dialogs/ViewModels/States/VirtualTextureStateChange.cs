namespace BG3PakViewer.Dialogs.ViewModels.States;

/// <summary>
///     The region of the virtual texture toolbox whose state changed.
/// </summary>
[Flags]
internal enum VirtualTextureStateChange
{
    /// <summary>
    ///     The index region changed.
    /// </summary>
    Index = 1 << 0,

    /// <summary>
    ///     The search region changed.
    /// </summary>
    Search = 1 << 1,

    /// <summary>
    ///     The preview region changed.
    /// </summary>
    Preview = 1 << 2,

    /// <summary>
    ///     The selection region changed.
    /// </summary>
    Selection = 1 << 3,

    /// <summary>
    ///     The export region changed.
    /// </summary>
    Export = 1 << 4
}
