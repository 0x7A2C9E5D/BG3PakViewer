using BG3PakViewer.ViewModels.States;

namespace BG3PakViewer.ViewModels;

/// <summary>
///     The lifecycle states of the main window: the loaded package, the preview pane, the tree
///     selection and the export run.
/// </summary>
internal sealed class MainWindowState
{
    private readonly Action<MainWindowStateChange> _onChanged;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MainWindowState" /> class.
    /// </summary>
    /// <param name="onChanged">Notifies the view model which region changed.</param>
    public MainWindowState(Action<MainWindowStateChange> onChanged)
    {
        _onChanged = onChanged;
    }

    private MainWindowLoadState Load { get; set; }

    private MainWindowPreviewState Preview { get; set; }

    private bool HasSelection { get; set; }

    private MainWindowExportState Export { get; set; }

    public bool IsLoading => Load == MainWindowLoadState.Loading;

    public bool IsBuildingPreview => Preview == MainWindowPreviewState.Building;

    /// <summary>
    ///     Whether a file or folder export is writing files.
    /// </summary>
    public bool IsExporting => Export is MainWindowExportState.FileExporting or MainWindowExportState.FolderExporting;

    /// <summary>
    ///     Whether the running export can be canceled; only a folder export has a cancellation source.
    /// </summary>
    public bool CanCancelExport => Export == MainWindowExportState.FolderExporting;

    /// <summary>
    ///     Whether the export command may run: start one with a selection, or cancel a running one.
    ///     While the target dialog is open the command is disabled, so no second dialog can be opened.
    /// </summary>
    public bool CanRunExportCommand => Export switch
    {
        MainWindowExportState.Idle => HasSelection,
        MainWindowExportState.FileExporting or MainWindowExportState.FolderExporting => true,
        _ => false
    };

    /// <summary>
    ///     Starts a package load, replacing whatever was loaded before.
    /// </summary>
    public void BeginLoad()
    {
        if (Load == MainWindowLoadState.Loading)
            throw new InvalidOperationException("Cannot start loading while a load is running.");
        Load = MainWindowLoadState.Loading;
    }

    /// <summary>
    ///     Ends the package load, publishing whether a package was loaded.
    /// </summary>
    /// <param name="success">Whether the last load published a tree.</param>
    public void EndLoad(bool success)
    {
        if (Load != MainWindowLoadState.Loading)
            throw new InvalidOperationException($"Cannot end loading from {Load}.");
        Load = success ? MainWindowLoadState.Loaded : MainWindowLoadState.Failed;
    }

    /// <summary>
    ///     Starts building a preview, replacing the one that is shown.
    /// </summary>
    public void BeginPreview()
    {
        if (Preview == MainWindowPreviewState.Building)
            throw new InvalidOperationException("Cannot start building a preview while one is building.");
        Preview = MainWindowPreviewState.Building;
    }

    /// <summary>
    ///     Ends a preview build, publishing whether a preview is now shown.
    /// </summary>
    /// <param name="shown">Whether a preview view model is published.</param>
    public void EndPreview(bool shown)
    {
        if (Preview != MainWindowPreviewState.Building)
            throw new InvalidOperationException($"Cannot end a preview build from {Preview}.");
        Preview = shown ? MainWindowPreviewState.Showing : MainWindowPreviewState.Empty;
    }

    /// <summary>
    ///     Clears the shown preview. A build in progress keeps its phase so a concurrent request
    ///     cannot start a second one.
    /// </summary>
    public void ClearPreview()
    {
        if (Preview == MainWindowPreviewState.Building) return;
        Preview = MainWindowPreviewState.Empty;
    }

    /// <summary>
    ///     Reflects whether a tree item is selected, which is what enables starting an export.
    /// </summary>
    /// <param name="hasSelection">Whether a tree item is selected.</param>
    public void SetSelection(bool hasSelection)
    {
        if (hasSelection == HasSelection) return;
        HasSelection = hasSelection;
        _onChanged(MainWindowStateChange.Selection);
    }

    /// <summary>
    ///     Opens the export workflow by asking for the target.
    /// </summary>
    public void BeginTargetPick()
    {
        if (Export != MainWindowExportState.Idle)
            throw new InvalidOperationException($"Cannot pick an export target from {Export}.");
        Export = MainWindowExportState.PickingTarget;
        _onChanged(MainWindowStateChange.Export);
    }

    /// <summary>
    ///     Closes the export workflow when the user cancels the target dialog.
    /// </summary>
    public void CancelTargetPick()
    {
        if (Export != MainWindowExportState.PickingTarget)
            throw new InvalidOperationException($"Cannot cancel a target pick from {Export}.");
        Export = MainWindowExportState.Idle;
        _onChanged(MainWindowStateChange.Export);
    }

    /// <summary>
    ///     Starts writing a single file once the target is known.
    /// </summary>
    public void BeginFileExport()
    {
        if (Export != MainWindowExportState.PickingTarget)
            throw new InvalidOperationException($"Cannot start a file export from {Export}.");
        Export = MainWindowExportState.FileExporting;
        _onChanged(MainWindowStateChange.Export);
    }

    /// <summary>
    ///     Starts writing a folder once the target is known.
    /// </summary>
    public void BeginFolderExport()
    {
        if (Export != MainWindowExportState.PickingTarget)
            throw new InvalidOperationException($"Cannot start a folder export from {Export}.");
        Export = MainWindowExportState.FolderExporting;
        _onChanged(MainWindowStateChange.Export);
    }

    /// <summary>
    ///     Ends the export run, whether it completed, failed or was canceled.
    /// </summary>
    public void EndExport()
    {
        if (!IsExporting)
            throw new InvalidOperationException($"Cannot end an export from {Export}.");
        Export = MainWindowExportState.Idle;
        _onChanged(MainWindowStateChange.Export);
    }
}
