using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace BG3PakViewer.Controls.ViewModels;

public partial class PlainTextPreviewViewModel : ObservableObject
{
    [ObservableProperty] public partial string? Data { get; set; }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnDataChanged(string? value)
    {
        Log.Information("PlainTextFilePreviewViewModel.DataChanged");
    }
}