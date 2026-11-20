using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing unsupported files.
/// </summary>
public partial class NotSupportedPreviewViewModel : ObservableObject
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NotSupportedPreviewViewModel" /> class.
    /// </summary>
    /// <param name="helpTextKey">The resource key of the help text to display.</param>
    public NotSupportedPreviewViewModel(string helpTextKey)
    {
        HelpTextKey = helpTextKey;
    }

    /// <summary>
    ///     The resource key of the help text to display.
    /// </summary>
    /// <remarks>
    ///     The key is resolved by the I18N binding in the template instead of being resolved here, so the text
    ///     follows a language change while the preview stays open.
    /// </remarks>
    [ObservableProperty]
    public partial string HelpTextKey { get; private set; }
}
