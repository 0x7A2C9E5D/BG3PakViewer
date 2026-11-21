namespace BG3PakViewer.Dialogs.ViewModels;

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

/// <summary>
///     The five orthogonal state regions of the virtual texture toolbox. Each async run carries a
///     generation: a run that finishes after a newer one started cannot publish a stale state.
/// </summary>
internal sealed class VirtualTextureDialogState
{
    /// <summary>
    ///     The states of the export region. Picking the folder is part of the export workflow so a
    ///     second export command cannot open a second folder dialog while the first one is up.
    /// </summary>
    public enum ExportPhase
    {
        Idle,
        PickingFolder,
        Exporting
    }

    /// <summary>
    ///     The states of the index region.
    /// </summary>
    public enum IndexPhase
    {
        Idle,
        Indexing,
        Ready
    }

    /// <summary>
    ///     The states of the preview region.
    /// </summary>
    public enum PreviewPhase
    {
        Empty,
        Loading,
        Loaded,
        Failed
    }

    /// <summary>
    ///     The states of the search region.
    /// </summary>
    public enum SearchPhase
    {
        Unfiltered,
        Filtered
    }

    /// <summary>
    ///     The states of the selection region.
    /// </summary>
    public enum SelectionPhase
    {
        // ReSharper disable once UnusedMember.Global
        Unknown,
        Checking,
        HasData,
        NoData
    }

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

    public IndexPhase Index { get; private set; }

    public SearchPhase Search { get; private set; }

    public PreviewPhase Preview { get; private set; }

    public SelectionPhase Selection { get; private set; }

    public ExportPhase Export { get; private set; }

    public bool IsIndexing => Index == IndexPhase.Indexing;

    public bool IsPreviewing => Preview == PreviewPhase.Loading;

    public bool IsExporting => Export == ExportPhase.Exporting;

    public bool IsBusy => IsIndexing || IsPreviewing || IsExporting;

    public bool CanClose => Export != ExportPhase.Exporting;

    /// <summary>
    ///     Starts the only indexing run of the dialog.
    /// </summary>
    public void BeginIndex()
    {
        if (Index != IndexPhase.Idle)
            throw new InvalidOperationException($"Cannot start indexing from {Index}.");
        Index = IndexPhase.Indexing;
        _onChanged(VirtualTextureStateChange.Index);
    }

    /// <summary>
    ///     Ends the indexing run. A canceled run never reached a published index, so it returns to idle.
    /// </summary>
    /// <param name="cancelled">Whether the dialog was closed while indexing.</param>
    public void EndIndex(bool cancelled)
    {
        if (Index != IndexPhase.Indexing)
            throw new InvalidOperationException($"Cannot end indexing from {Index}.");
        Index = cancelled ? IndexPhase.Idle : IndexPhase.Ready;
        _onChanged(VirtualTextureStateChange.Index);
    }

    /// <summary>
    ///     Reflects the filter the base search view model published.
    /// </summary>
    /// <param name="filtered">Whether a non-empty filter is active.</param>
    public void SetSearchFiltered(bool filtered)
    {
        var next = filtered ? SearchPhase.Filtered : SearchPhase.Unfiltered;
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
        Preview = PreviewPhase.Loading;
        _onChanged(VirtualTextureStateChange.Preview);
        return _previewRun;
    }

    /// <summary>
    ///     Publishes the result of a preview load, unless a newer load replaced it.
    /// </summary>
    /// <param name="run">The generation of the finishing run.</param>
    /// <param name="result">The state the load reached.</param>
    public void EndPreview(int run, PreviewPhase result)
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
        Preview = PreviewPhase.Empty;
        _onChanged(VirtualTextureStateChange.Preview);
    }

    /// <summary>
    ///     Starts a selection check, replacing the previous one.
    /// </summary>
    /// <returns>The generation of the new check.</returns>
    public int BeginSelectionCheck()
    {
        _selectionRun++;
        Selection = SelectionPhase.Checking;
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
        Selection = hasData ? SelectionPhase.HasData : SelectionPhase.NoData;
        _onChanged(VirtualTextureStateChange.Selection);
    }

    /// <summary>
    ///     Opens the export workflow by asking for the target folder.
    /// </summary>
    public void BeginFolderPick()
    {
        if (Export != ExportPhase.Idle)
            throw new InvalidOperationException($"Cannot pick an export folder from {Export}.");
        Export = ExportPhase.PickingFolder;
        _onChanged(VirtualTextureStateChange.Export);
    }

    /// <summary>
    ///     Closes the export workflow when the user cancels the folder dialog.
    /// </summary>
    public void CancelFolderPick()
    {
        if (Export != ExportPhase.PickingFolder)
            throw new InvalidOperationException($"Cannot cancel a folder pick from {Export}.");
        Export = ExportPhase.Idle;
        _onChanged(VirtualTextureStateChange.Export);
    }

    /// <summary>
    ///     Starts writing files once the target folder is known.
    /// </summary>
    public void BeginExport()
    {
        if (Export != ExportPhase.PickingFolder)
            throw new InvalidOperationException($"Cannot start exporting from {Export}.");
        Export = ExportPhase.Exporting;
        _onChanged(VirtualTextureStateChange.Export);
    }

    /// <summary>
    ///     Ends the export run, whether it completed, failed or was canceled.
    /// </summary>
    public void EndExport()
    {
        if (Export != ExportPhase.Exporting)
            throw new InvalidOperationException($"Cannot end exporting from {Export}.");
        Export = ExportPhase.Idle;
        _onChanged(VirtualTextureStateChange.Export);
    }
}
