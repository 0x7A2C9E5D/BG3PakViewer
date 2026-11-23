using BG3PakViewer.Dialogs.ViewModels.States;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     The five orthogonal state regions of the virtual texture toolbox. Each async run carries a
///     generation: a run that finishes after a newer one started cannot publish a stale state.
/// </summary>
internal sealed class VirtualTextureDialogState
{
    private readonly Action<VirtualTextureStateChange> _onChanged;
    private int _previewRun;
    private int _selectionRun;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VirtualTextureDialogState" /> class.
    /// </summary>
    /// <param name="onChanged">Notifies the view model which region changed.</param>
    public VirtualTextureDialogState(Action<VirtualTextureStateChange> onChanged)
    {
        _onChanged = onChanged;
    }

    private VirtualTextureIndexState Index { get; set; }

    public VirtualTextureSearchState Search { get; private set; }

    private VirtualTexturePreviewState Preview { get; set; }

    public VirtualTextureSelectionState Selection { get; private set; }

    public VirtualTextureExportState Export { get; private set; }

    public bool IsIndexing => Index == VirtualTextureIndexState.Indexing;

    public bool IsPreviewing => Preview == VirtualTexturePreviewState.Loading;

    public bool IsExporting => Export == VirtualTextureExportState.Exporting;

    public bool IsBusy => IsIndexing || IsPreviewing || IsExporting;

    public bool CanClose => Export != VirtualTextureExportState.Exporting;

    /// <summary>
    ///     Starts the only indexing run of the dialog.
    /// </summary>
    public void BeginIndex()
    {
        if (Index != VirtualTextureIndexState.Idle)
            throw new InvalidOperationException($"Cannot start indexing from {Index}.");
        Index = VirtualTextureIndexState.Indexing;
        _onChanged(VirtualTextureStateChange.Index);
    }

    /// <summary>
    ///     Ends the indexing run. A canceled run never reached a published index, so it returns to idle.
    /// </summary>
    /// <param name="cancelled">Whether the dialog was closed while indexing.</param>
    public void EndIndex(bool cancelled)
    {
        if (Index != VirtualTextureIndexState.Indexing)
            throw new InvalidOperationException($"Cannot end indexing from {Index}.");
        Index = cancelled ? VirtualTextureIndexState.Idle : VirtualTextureIndexState.Ready;
        _onChanged(VirtualTextureStateChange.Index);
    }

    /// <summary>
    ///     Reflects the filter the base search view model published.
    /// </summary>
    /// <param name="filtered">Whether a non-empty filter is active.</param>
    public void SetSearchFiltered(bool filtered)
    {
        var next = filtered ? VirtualTextureSearchState.Filtered : VirtualTextureSearchState.Unfiltered;
        if (next == Search) return;
        Search = next;
        _onChanged(VirtualTextureStateChange.Search);
    }

    /// <summary>
    ///     Starts a preview load, replacing whatever the region held before.
    /// </summary>
    /// <returns>The generation of the new run.</returns>
    public int BeginPreview()
    {
        _previewRun++;
        Preview = VirtualTexturePreviewState.Loading;
        _onChanged(VirtualTextureStateChange.Preview);
        return _previewRun;
    }

    /// <summary>
    ///     Publishes the result of a preview load, unless a newer load replaced it.
    /// </summary>
    /// <param name="run">The generation of the finishing run.</param>
    /// <param name="result">The state the load reached.</param>
    public void EndPreview(int run, VirtualTexturePreviewState result)
    {
        if (run != _previewRun) return;
        Preview = result;
        _onChanged(VirtualTextureStateChange.Preview);
    }

    /// <summary>
    ///     Clears the preview and invalidates the load that may still be running.
    /// </summary>
    public void ClearPreview()
    {
        _previewRun++;
        Preview = VirtualTexturePreviewState.Empty;
        _onChanged(VirtualTextureStateChange.Preview);
    }

    /// <summary>
    ///     Starts a selection check, replacing the previous one.
    /// </summary>
    /// <returns>The generation of the new check.</returns>
    public int BeginSelectionCheck()
    {
        _selectionRun++;
        Selection = VirtualTextureSelectionState.Checking;
        _onChanged(VirtualTextureStateChange.Selection);
        return _selectionRun;
    }

    /// <summary>
    ///     Publishes the result of a selection check, unless a newer check replaced it.
    /// </summary>
    /// <param name="run">The generation of the finishing check.</param>
    /// <param name="hasData">Whether a selected layer holds data.</param>
    public void EndSelectionCheck(int run, bool hasData)
    {
        if (run != _selectionRun) return;
        Selection = hasData ? VirtualTextureSelectionState.HasData : VirtualTextureSelectionState.NoData;
        _onChanged(VirtualTextureStateChange.Selection);
    }

    /// <summary>
    ///     Opens the export workflow by asking for the target folder.
    /// </summary>
    public void BeginFolderPick()
    {
        if (Export != VirtualTextureExportState.Idle)
            throw new InvalidOperationException($"Cannot pick an export folder from {Export}.");
        Export = VirtualTextureExportState.PickingFolder;
        _onChanged(VirtualTextureStateChange.Export);
    }

    /// <summary>
    ///     Closes the export workflow when the user cancels the folder dialog.
    /// </summary>
    public void CancelFolderPick()
    {
        if (Export != VirtualTextureExportState.PickingFolder)
            throw new InvalidOperationException($"Cannot cancel a folder pick from {Export}.");
        Export = VirtualTextureExportState.Idle;
        _onChanged(VirtualTextureStateChange.Export);
    }

    /// <summary>
    ///     Starts writing files once the target folder is known.
    /// </summary>
    public void BeginExport()
    {
        if (Export != VirtualTextureExportState.PickingFolder)
            throw new InvalidOperationException($"Cannot start exporting from {Export}.");
        Export = VirtualTextureExportState.Exporting;
        _onChanged(VirtualTextureStateChange.Export);
    }

    /// <summary>
    ///     Ends the export run, whether it completed, failed or was canceled.
    /// </summary>
    public void EndExport()
    {
        if (Export != VirtualTextureExportState.Exporting)
            throw new InvalidOperationException($"Cannot end exporting from {Export}.");
        Export = VirtualTextureExportState.Idle;
        _onChanged(VirtualTextureStateChange.Export);
    }
}
