using BG3PakViewer.Extensions;
using BG3PakViewer.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HelixToolkit.Wpf.SharpDX;
using LSLib.Granny.Model;

namespace BG3PakViewer.Controls.ViewModels;

public partial class Model3DPreviewViewModel : ObservableObject
{
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    [ObservableProperty] public partial Root? Model { get; set; }

    // ReSharper disable once MemberCanBeMadeStatic.Global
    [ObservableProperty] public partial string[]? Meshes { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    // ReSharper disable once MemberCanBeMadeStatic.Global
    public partial int SelectedModelIndex { get; set; } = -1;

    [ObservableProperty] public partial ObservableElement3DCollection Models { get; set; } = [];

    partial void OnModelChanged(Root? value)
    {
        if (value?.Meshes == null)
        {
            SelectedModelIndex = -1;
            return;
        }

        Meshes = [.. value.Meshes.Select(x => x.Name)];
        SelectedModelIndex = Meshes.Any() ? 0 : -1;
    }

    private bool CanPreview()
    {
        return SelectedModelIndex != -1;
    }

    [RelayCommand(CanExecute = nameof(CanPreview))]
    private void Preview()
    {
        Models.Clear();
        Models.Add(Model!.ToGeometry3D(SelectedModelIndex).ToGeometryModel3D());
    }

    [RelayCommand]
    private static void Zoom()
    {
        WeakReferenceMessenger.Default.Send(string.Empty, MessageTokens.ZoomExtents);
    }
}