using System.Collections.ObjectModel;
using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Shared.ViewModels;
using BG3PakViewer.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using LSLib.LS.Story;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing story scripts.
/// </summary>
public partial class StoryScriptsPreviewViewModel : SearchFilterViewModel
{
    private readonly IAppSettings _appSettings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StoryScriptsPreviewViewModel" /> class.
    /// </summary>
    /// <param name="appSettings"></param>
    public StoryScriptsPreviewViewModel(IAppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    /// <summary>
    ///     The story to preview.
    /// </summary>
    [ObservableProperty]
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    public partial Story? Story { get; set; }

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
    ///     Resets the view model when the story is changed.
    /// </summary>
    /// <param name="value"></param>
    partial void OnStoryChanged(Story? value)
    {
        Goals.Clear();
        if (value is null) return;
        foreach (var goal in value.Goals.Values)
            Goals.Add(new StoryScriptsGoalItemViewModel { Goal = goal });
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
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedGoalChanged(StoryScriptsGoalItemViewModel? value)
    {
        _ = DecompileScriptsAsync();
    }

    /// <summary>
    ///     Decompiles the scripts.
    /// </summary>
    private async Task DecompileScriptsAsync()
    {
        if (SelectedGoal is null)
        {
            Scripts = null;
            return;
        }

        await using var writer = new StringWriter();
        SelectedGoal.Goal?.MakeScript(writer, Story);
        Scripts = await Task.Run(() => TextOperations.TruncateToLines(writer.ToString(), _appSettings.MaxPreviewLines));
    }

}