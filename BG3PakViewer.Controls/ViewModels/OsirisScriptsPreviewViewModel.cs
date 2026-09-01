using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
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
        GoalsView = CollectionViewSource.GetDefaultView(Goals);
        GoalsView.Filter = FilterGoal;
        WeakReferenceMessenger.Default.Register<SearchMessage>(this, (_, message) => OnSearchMessage(message));
    }

    [ObservableProperty]
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    public partial Story? Story { get; set; }

    [ObservableProperty] public partial OsirisGoalItemViewModel? SelectedGoal { get; set; }

    private ObservableCollection<OsirisGoalItemViewModel> Goals { get; } = [];

    /// <summary>Filtered view of <see cref="Goals" />, driven by <see cref="SearchText" />.</summary>
    public ICollectionView GoalsView { get; }

    [ObservableProperty] public partial string? Scripts { get; private set; }

    [ObservableProperty] private partial string? SearchText { get; set; }

    partial void OnStoryChanged(Story? value)
    {
        Goals.Clear();
        if (value is null) return;
        foreach (var goal in value.Goals.Values)
            Goals.Add(new OsirisGoalItemViewModel { Goal = goal });
        GoalsView.Refresh();
    }

    private void OnSearchMessage(SearchMessage message)
    {
        SearchText = string.IsNullOrEmpty(message.Text) ? null : message.Text;
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSearchTextChanged(string? value)
    {
        GoalsView.Refresh();
    }

    private bool FilterGoal(object item)
    {
        return string.IsNullOrWhiteSpace(SearchText) ||
               ((OsirisGoalItemViewModel)item).Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
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