using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Controls.ViewModels;

public partial class NotSupportedFileViewModel : ObservableObject
{
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    [ObservableProperty] public partial string? HelpText { get; set; }
}