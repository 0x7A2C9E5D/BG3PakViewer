using System.Collections.ObjectModel;
using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Controls.ViewModels.States;
using BG3PakViewer.Shared.ViewModels;
using BG3PakViewer.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using HanumanInstitute.MvvmDialogs;
using LSLib.LS.Story;
using Serilog;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing story scripts.
/// </summary>
/// <remarks>
///     The class is split by responsibility: this file holds the goals and the decompiled script
///     of the selected one; <c>State</c> tracks the phases of the tool and translates their changes
///     into notifications, the sibling files export the scripts and talk to the user.
/// </remarks>
public partial class StoryScriptsPreviewViewModel : SearchFilterViewModel
{
    private readonly IAppSettings _appSettings;
    private readonly IDialogService _dialogService;
    private readonly IPreviewExportService _exportService;
    private readonly StoryScriptsPreviewState _state;

    /// <summary>
    ///     Cancels to decompile of a goal that a newer selection replaced.
    /// </summary>
    private CancellationTokenSource? _scriptsCancellation;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StoryScriptsPreviewViewModel" /> class.
    /// </summary>
    /// <param name="story">The story to preview.</param>
    /// <param name="appSettings">The settings holding the default export directory.</param>
    /// <param name="exportService">The service the export commands hand the decompiled scripts to.</param>
    /// <param name="dialogService">The service used to ask for the target folder and report the result.</param>
    public StoryScriptsPreviewViewModel(Story story,
        IAppSettings appSettings,
        IPreviewExportService exportService,
        IDialogService dialogService)
    {
        _appSettings = appSettings;
        _exportService = exportService;
        _dialogService = dialogService;
        _state = new StoryScriptsPreviewState(OnStateChanged);
        Story = story;

        foreach (var goal in story.Goals.Values)
            Goals.Add(new StoryScriptsGoalItemViewModel { Goal = goal });
        SelectedGoal = Goals.FirstOrDefault();
    }

    /// <summary>
    ///     The story to preview.
    /// </summary>
    [ObservableProperty]
    // ReSharper disable once UnusedMember.Global
    public partial Story Story { get; private set; }

    /// <summary>
    ///     The currently selected goal.
    /// </summary>
    [ObservableProperty]
    public partial StoryScriptsGoalItemViewModel? SelectedGoal { get; set; }

    /// <summary>
    ///     Full goal list. Filtering is a view concern: the view applies <c>ItemFilter</c> to this
    ///     collection's collection view, which keeps WPF's ICollectionView out of the view model.
    /// </summary>
    public ObservableCollection<StoryScriptsGoalItemViewModel> Goals { get; } = [];

    /// <summary>
    ///     The decompiled scripts.
    /// </summary>
    [ObservableProperty]
    public partial string? Scripts { get; private set; }

    /// <summary>
    ///     Whether the script of the selected goal is being decompiled.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public bool IsPreviewing => _state.IsPreviewing;

    /// <summary>
    ///     Builds the goal filter for the given search text.
    /// </summary>
    /// <param name="searchText"></param>
    /// <returns></returns>
    protected override Predicate<object>? BuildFilter(string? searchText)
    {
        return string.IsNullOrWhiteSpace(searchText)
            ? null
            : item => item is StoryScriptsGoalItemViewModel goal &&
                      goal.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Decompiles the scripts when the selected goal changes.
    /// </summary>
    /// <param name="value"></param>
    partial void OnSelectedGoalChanged(StoryScriptsGoalItemViewModel? value)
    {
        _state.SetSelection(value is not null);
        _ = ShowScriptsAsync(value);
    }

    /// <summary>
    ///     Shows the script of a goal, decompiling it off the UI thread.
    /// </summary>
    /// <param name="goal">The goal to decompile, or <see langword="null" /> to blank the script.</param>
    /// <returns>A task that completes once the script is shown or discarded.</returns>
    /// <remarks>
    ///     This runs from a property change, i.e. on the UI thread, and it awaits without
    ///     <c>ConfigureAwait(false)</c>, so <see cref="Scripts" /> is written back on the UI thread the
    ///     preview is bound to. Decompiling a goal takes long enough for a later selection to overtake
    ///     it, so the previous decompile is canceled and the state drops its result.
    /// </remarks>
    private async Task ShowScriptsAsync(StoryScriptsGoalItemViewModel? goal)
    {
        var previous = _scriptsCancellation;
        _scriptsCancellation = null;

        if (goal?.Goal is not { } target)
        {
            _state.ClearPreview();
            Scripts = null;
            if (previous is not null) await previous.CancelAsync();
            return;
        }

        var cts = new CancellationTokenSource();
        _scriptsCancellation = cts;
        var run = _state.BeginPreview();
        var maxLines = _appSettings.MaxPreviewLines;

        if (previous is not null) await previous.CancelAsync();

        var phase = StoryScriptsPreviewState.PreviewPhase.Empty;
        try
        {
            var scripts = await Task.Run(() => DecompileScript(target, maxLines), cts.Token);
            if (cts.IsCancellationRequested) return;

            Scripts = scripts;
            phase = StoryScriptsPreviewState.PreviewPhase.Loaded;
        }
        catch (OperationCanceledException)
        {
            // A newer selection replaced this decompile; the state drops the result.
        }
        catch (Exception ex)
        {
            if (cts.IsCancellationRequested) return;
            Log.Error(ex, "Failed to decompile the script of goal: {Goal}", target.Name);
            Scripts = null;
            phase = StoryScriptsPreviewState.PreviewPhase.Failed;
        }
        finally
        {
            _state.EndPreview(run, phase);
        }
    }

    /// <summary>
    ///     Cancels the running decompile when the preview is released.
    /// </summary>
    /// <param name="disposing">Whether managed resources are released.</param>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _ = _scriptsCancellation?.CancelAsync();
    }

    /// <summary>
    ///     Decompiles one goal and truncates the preview to the configured line limit.
    /// </summary>
    /// <param name="goal">The goal to decompile.</param>
    /// <param name="maxLines">The number of lines the preview shows.</param>
    /// <returns>The script text.</returns>
    private static string DecompileScript(Goal goal, int maxLines)
    {
        using var writer = new StringWriter();
        goal.MakeScript(writer, goal.Story);
        return TextOperations.TruncateToLines(writer.ToString(), maxLines);
    }
}
