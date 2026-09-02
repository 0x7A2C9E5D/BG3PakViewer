using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace BG3PakViewer.Controls.ViewModels;

public partial class PlainTextPreviewViewModel : ObservableObject
{
    [ObservableProperty] public partial string? Text { get; set; }

    partial void OnTextChanged(string? value)
    {
        // Text is replaced on every selection and carries no diagnostic value on its own,
        // so it is recorded at debug level only.
        Log.Debug("Text preview changed: {Length} characters", value?.Length ?? 0);
    }
}
