using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace BG3PakViewer.Controls.ViewModels;

public partial class PlainTextPreviewViewModel : ObservableObject
{
    [ObservableProperty] public partial string? Text { get; set; }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnTextChanged(string? value)
    {
        Log.Information("PlainTextPreviewViewModel.TextChanged");
    }
}
