using CommunityToolkit.Mvvm.ComponentModel;
using LSLib.LS.Story;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for a single goal in a story script.
/// </summary>
public class StoryScriptsGoalItemViewModel : ObservableObject
{
    /// <summary>
    ///     The goal.
    /// </summary>
    public Goal? Goal { get; init; }

    /// <summary>
    ///     The goal name.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public string Name => Goal?.Name ?? "Unknown";
}