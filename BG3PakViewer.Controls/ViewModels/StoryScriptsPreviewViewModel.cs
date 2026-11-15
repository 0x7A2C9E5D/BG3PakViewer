using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using BG3PakViewer.Contracts;
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
///     of the selected one, the sibling files export the scripts and talk to the user.
/// </remarks>
public partial class StoryScriptsPreviewViewModel : SearchFilterViewModel
{
    private readonly IAppSettings _appSettings;
    private readonly IDialogService _dialogService;
    private readonly IPreviewExportService _exportService;

    /// <summary>
    ///     Identifies the decompile that owns <see cref="Scripts" />: a decompile that was started for
    ///     an earlier selection compares against it and drops its own result.
    /// </summary>
    private int _scriptsVersion;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StoryScriptsPreviewViewModel" /> class.
    /// </summary>
    /// <param name="appSettings">The settings holding the default export directory.</param>
    /// <param name="exportService">The service the export commands hand the decompiled scripts to.</param>
    /// <param name="dialogService">The service used to ask for the target folder and report the result.</param>
    public StoryScriptsPreviewViewModel(IAppSettings appSettings,
        IPreviewExportService exportService,
        IDialogService dialogService)
    {
        _appSettings = appSettings;
        _exportService = exportService;
        _dialogService = dialogService;
    }

    /// <summary>
    ///     The story to preview.
    /// </summary>
    [ObservableProperty]
    // ReSharper disable once UnusedMember.Global
    public partial Story? Story { get; set; }

    /// <summary>
    ///     The currently selected goal.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportSelectedCommand))]
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
    ///     Keeps the search-results export command in sync with the goals the current search matches.
    /// </summary>
    /// <param name="e"></param>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(ItemFilter))
            ExportSearchResultsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Resets the view model when the story is changed.
    /// </summary>
    /// <param name="value"></param>
    partial void OnStoryChanged(Story? value)
    {
        Goals.Clear();
        if (value is not null)
            foreach (var goal in value.Goals.Values)
                Goals.Add(new StoryScriptsGoalItemViewModel { Goal = goal });
        SelectedGoal = Goals.FirstOrDefault();
    }

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
    ///     it, so a result is only shown while no newer decompile was requested.
    /// </remarks>
    private async Task ShowScriptsAsync(StoryScriptsGoalItemViewModel? goal)
    {
        var version = ++_scriptsVersion;
        if (goal?.Goal is not { } target)
        {
            Scripts = null;
            return;
        }

        var maxLines = _appSettings.MaxPreviewLines;
        try
        {
            var scripts = await Task.Run(() => DecompileScript(target, maxLines));
            if (version == _scriptsVersion) Scripts = scripts;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to decompile the script of goal: {Goal}", target.Name);
            if (version == _scriptsVersion) Scripts = null;
        }
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
