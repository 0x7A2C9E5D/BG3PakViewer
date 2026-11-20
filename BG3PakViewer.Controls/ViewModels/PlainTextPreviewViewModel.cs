using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing plain text files.
/// </summary>
public partial class PlainTextPreviewViewModel : ObservableObject
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PlainTextPreviewViewModel" /> class.
    /// </summary>
    /// <param name="text">The text to display.</param>
    public PlainTextPreviewViewModel(string text)
    {
        Text = text;
    }

    /// <summary>
    ///     The text to display.
    /// </summary>
    [ObservableProperty]
    public partial string Text { get; private set; }
}
