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

public partial class OsirisScriptsPreviewViewModel : DisposableViewModel
{
    private readonly IAppSettings _appSettings;

    public OsirisScriptsPreviewViewModel(IAppSettings appSettings)
    {
        _appSettings = appSettings;
        WeakReferenceMessenger.Default.Register<SearchMessage>(this, (_, message) => OnSearchMessage(message));
    }

    [ObservableProperty]
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    public partial Story? Story { get; set; }

    [ObservableProperty] public partial OsirisGoalItemViewModel? SelectedGoal { get; set; }

    /// <summary>
    ///     Full goal list. Filtering is a view concern: the view applies <c>GoalFilter</c> to this
    ///     collection's collection view, which keeps WPF's ICollectionView out of the view model.
    /// </summary>
    public ObservableCollection<OsirisGoalItemViewModel> Goals { get; } = [];

    [ObservableProperty] public partial string? Scripts { get; private set; }

    // ReSharper disable once UnusedMember.Local
    [ObservableProperty] private partial string? SearchText { get; set; }

    /// <summary>
    ///     Predicate the view applies to <see cref="Goals" />; null shows every goal. Rebuilt whenever
    ///     <c>SearchText</c> changes, which the view picks up and re-applies.
    /// </summary>
    [ObservableProperty]
    public partial Predicate<object>? GoalFilter { get; private set; }

    partial void OnStoryChanged(Story? value)
    {
        Goals.Clear();
        if (value is null) return;
        foreach (var goal in value.Goals.Values)
            Goals.Add(new OsirisGoalItemViewModel { Goal = goal });
    }

    private void OnSearchMessage(SearchMessage message)
    {
        SearchText = string.IsNullOrEmpty(message.Text) ? null : message.Text;
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSearchTextChanged(string? value)
    {
        GoalFilter = string.IsNullOrWhiteSpace(value)
            ? null
            : item => item is OsirisGoalItemViewModel goal &&
                      goal.Name.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedGoalChanged(OsirisGoalItemViewModel? value)
    {
        _ = DecompileScriptsAsync();
    }

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