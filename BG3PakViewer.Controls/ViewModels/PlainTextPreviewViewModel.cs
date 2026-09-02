using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Controls.ViewModels;

public partial class PlainTextPreviewViewModel : ObservableObject
{
    [ObservableProperty] public partial string? Text { get; set; }
}
