using CommunityToolkit.Mvvm.ComponentModel;
using LSLib.LS.Story;

namespace BG3PakViewer.Controls.ViewModels;

public class GoalViewModel : ObservableObject
{
    public Goal? Goal { get; init; }

    public string Name => Goal?.Name ?? "Unknown";
}