using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing plain text files.
/// </summary>
public partial class PlainTextPreviewViewModel : ObservableObject
{
    /// <summary>
    ///     The text to display.
    /// </summary>
    [ObservableProperty] public partial string? Text { get; set; }
}