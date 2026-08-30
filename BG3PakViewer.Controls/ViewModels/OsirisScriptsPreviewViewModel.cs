using System.Collections.ObjectModel;
using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using LSLib.LS.Story;

namespace BG3PakViewer.Controls.ViewModels;

public partial class OsirisScriptsPreviewViewModel(IAppSettings appSettings) : ObservableObject
{
    [ObservableProperty]
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    public partial Story? Story { get; set; }

    [ObservableProperty] public partial OsirisGoalItemViewModel? SelectedGoal { get; set; }

    public ObservableCollection<OsirisGoalItemViewModel> Goals { get; } = [];

    [ObservableProperty] public partial string? Scripts { get; private set; }

    partial void OnStoryChanged(Story? value)
    {
        Goals.Clear();
        if (value is null) return;
        foreach (var goal in value.Goals.Values)
            Goals.Add(new OsirisGoalItemViewModel { Goal = goal });
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
        Scripts = await TextOperations.TruncateToLinesAsync(writer.ToString(), appSettings.MaxPreviewLines);
    }
}
