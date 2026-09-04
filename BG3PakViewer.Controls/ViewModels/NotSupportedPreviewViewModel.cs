using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing unsupported files.
/// </summary>
public partial class NotSupportedPreviewViewModel : ObservableObject
{
    /// <summary>
    ///     The help text to display.
    /// </summary>
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    [ObservableProperty] public partial string? HelpText { get; set; }
}