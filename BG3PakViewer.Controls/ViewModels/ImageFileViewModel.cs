using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace BG3PakViewer.Controls.ViewModels;

public partial class ImageFileViewModel : ObservableObject
{
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    [ObservableProperty] public partial ImageSource? Data { get; set; }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnDataChanged(ImageSource? value)
    {
        Log.Information("ImageFileViewModel.DataChanged");
    }
}