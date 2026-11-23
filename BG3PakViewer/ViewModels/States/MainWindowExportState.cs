namespace BG3PakViewer.ViewModels.States;

/// <summary>
///     The states of the export region. Picking the target is part of the run so a second export
///     command cannot open a second dialog while the first one is up.
/// </summary>
internal enum MainWindowExportState
{
    Idle,
    PickingTarget,
    FileExporting,
    FolderExporting
}
