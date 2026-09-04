using System.Collections.ObjectModel;
using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Messaging;
using BG3PakViewer.Shared.ViewModels;
using BG3PakViewer.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using LSLib.LS.Story;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing story scripts.
/// </summary>
public partial class StoryScriptsPreviewViewModel : DisposableViewModel
{
    private readonly IAppSettings _appSettings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StoryScriptsPreviewViewModel"/> class.
    /// </summary>
    /// <param name="appSettings"></param>
    public StoryScriptsPreviewViewModel(IAppSettings appSettings)
    {
        _appSettings = appSettings;
        WeakReferenceMessenger.Default.Register<SearchMessage>(this, (_, message) => OnSearchMessage(message));
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
    [ObservableProperty] public partial StoryScriptsGoalItemViewModel? SelectedGoal { get; set; }

    /// <summary>
    ///     Full goal list. Filtering is a view concern: the view applies <c>GoalFilter</c> to this
    ///     collection's collection view, which keeps WPF's ICollectionView out of the view model.
    /// </summary>
    public ObservableCollection<StoryScriptsGoalItemViewModel> Goals { get; } = [];

    /// <summary>
    ///     The decompiled scripts.
    /// </summary>
    [ObservableProperty] public partial string? Scripts { get; private set; }

    /// <summary>
    ///     The current search text.
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    [ObservableProperty] private partial string? SearchText { get; set; }

    /// <summary>
    ///     Predicate the view applies to <see cref="Goals" />; null shows every goal. Rebuilt whenever
    ///     <c>SearchText</c> changes, which the view picks up and re-applies.
    /// </summary>
    [ObservableProperty]
    public partial Predicate<object>? GoalFilter { get; private set; }

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
    ///     Handles search messages.
    /// </summary>
    /// <param name="message"></param>
    private void OnSearchMessage(SearchMessage message)
    {
        SearchText = string.IsNullOrEmpty(message.Text) ? null : message.Text;
    }

    /// <summary>
    ///     Rebuilds the filter when the search text changes.
    /// </summary>
    /// <param name="value"></param>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSearchTextChanged(string? value)
    {
        GoalFilter = string.IsNullOrWhiteSpace(value)
            ? null
            : item => item is StoryScriptsGoalItemViewModel goal &&
                      goal.Name.Contains(value, StringComparison.OrdinalIgnoreCase);
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
        Scripts = await TextOperations.TruncateToLinesAsync(writer.ToString(), _appSettings.MaxPreviewLines);
    }

    protected override void Dispose(bool disposing)
    {
        WeakReferenceMessenger.Default.Unregister<SearchMessage>(this);
        base.Dispose(disposing);
    }
}