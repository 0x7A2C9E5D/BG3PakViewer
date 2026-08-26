using CommunityToolkit.Mvvm.ComponentModel;
using LSLib.LS.Story;

namespace BG3PakViewer.Controls.ViewModels;

public class OsirisGoalItemViewModel : ObservableObject
{
    public Goal? Goal { get; init; }

    // ReSharper disable once UnusedMember.Global
    public string Name => Goal?.Name ?? "Unknown";
}