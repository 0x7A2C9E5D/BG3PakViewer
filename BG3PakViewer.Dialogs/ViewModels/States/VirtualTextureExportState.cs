namespace BG3PakViewer.Dialogs.ViewModels.States;

/// <summary>
///     The states of the virtual texture export region. Picking the folder is part of the export
///     workflow so a second export command cannot open a second folder dialog while the first one is up.
/// </summary>
internal enum VirtualTextureExportState
{
    Idle,
    PickingFolder,
    Exporting
}
