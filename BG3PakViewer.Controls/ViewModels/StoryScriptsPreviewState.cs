namespace BG3PakViewer.Controls.ViewModels;

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

/// <summary>
///     The four orthogonal state regions of the story scripts preview. Each decompile carries a
///     generation: a run that finishes after a newer one started cannot publish a stale script.
/// </summary>
internal sealed class StoryScriptsPreviewState
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
    ///     The states of the script region.
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

    private readonly Action<StoryScriptsStateChange> _onChanged;
    private int _previewRun;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StoryScriptsPreviewState" /> class.
    /// </summary>
    /// <param name="onChanged">Notifies the view model which region changed.</param>
    public StoryScriptsPreviewState(Action<StoryScriptsStateChange> onChanged)
    {
        _onChanged = onChanged;
    }

    public PreviewPhase Preview { get; private set; }

    public SearchPhase Search { get; private set; }

    public bool HasSelection { get; private set; }

    public ExportPhase Export { get; private set; }

    public bool IsPreviewing => Preview == PreviewPhase.Loading;

    public bool IsExporting => Export == ExportPhase.Exporting;

    /// <summary>
    ///     Whether an export command may start: no folder dialog is open and no export is running.
    /// </summary>
    public bool CanExport => Export == ExportPhase.Idle;

    /// <summary>
    ///     Starts a decompiled, replacing whatever the script region held before.
    /// </summary>
    /// <returns>The generation of the new run.</returns>
    public int BeginPreview()
    {
        _previewRun++;
        Preview = PreviewPhase.Loading;
        _onChanged(StoryScriptsStateChange.Preview);
        return _previewRun;
    }

    /// <summary>
    ///     Publishes the result of a decompiled, unless a newer run replaced it.
    /// </summary>
    /// <param name="run">The generation of the finishing run.</param>
    /// <param name="result">The state the run reached.</param>
    public void EndPreview(int run, PreviewPhase result)
    {
        if (run != _previewRun) return;
        Preview = result;
        _onChanged(StoryScriptsStateChange.Preview);
    }

    /// <summary>
    ///     Clears the script region and invalidates to decompile that may still be running.
    /// </summary>
    public void ClearPreview()
    {
        _previewRun++;
        Preview = PreviewPhase.Empty;
        _onChanged(StoryScriptsStateChange.Preview);
    }

    /// <summary>
    ///     Reflects the filter the base view model published. It notifies even when the phase is
    ///     unchanged, because a different predicate changes which goals the search export matches.
    /// </summary>
    /// <param name="filtered">Whether a non-empty filter is active.</param>
    public void SetSearchFiltered(bool filtered)
    {
        Search = filtered ? SearchPhase.Filtered : SearchPhase.Unfiltered;
        _onChanged(StoryScriptsStateChange.Search);
    }

    /// <summary>
    ///     Reflects whether a goal is selected, which is what exporting the current script handles.
    /// </summary>
    /// <param name="hasSelection">Whether a goal is selected.</param>
    public void SetSelection(bool hasSelection)
    {
        if (hasSelection == HasSelection) return;
        HasSelection = hasSelection;
        _onChanged(StoryScriptsStateChange.Selection);
    }

    /// <summary>
    ///     Opens the export workflow by asking for the target folder.
    /// </summary>
    public void BeginFolderPick()
    {
        if (Export != ExportPhase.Idle)
            throw new InvalidOperationException($"Cannot pick an export folder from {Export}.");
        Export = ExportPhase.PickingFolder;
        _onChanged(StoryScriptsStateChange.Export);
    }

    /// <summary>
    ///     Closes the export workflow when the user cancels the folder dialog.
    /// </summary>
    public void CancelFolderPick()
    {
        if (Export != ExportPhase.PickingFolder)
            throw new InvalidOperationException($"Cannot cancel a folder pick from {Export}.");
        Export = ExportPhase.Idle;
        _onChanged(StoryScriptsStateChange.Export);
    }

    /// <summary>
    ///     Starts writing files once the target folder is known.
    /// </summary>
    public void BeginExport()
    {
        if (Export != ExportPhase.PickingFolder)
            throw new InvalidOperationException($"Cannot start exporting from {Export}.");
        Export = ExportPhase.Exporting;
        _onChanged(StoryScriptsStateChange.Export);
    }

    /// <summary>
    ///     Ends the export run, whether it completed, failed or was canceled.
    /// </summary>
    public void EndExport()
    {
        if (Export != ExportPhase.Exporting)
            throw new InvalidOperationException($"Cannot end exporting from {Export}.");
        Export = ExportPhase.Idle;
        _onChanged(StoryScriptsStateChange.Export);
    }
}
