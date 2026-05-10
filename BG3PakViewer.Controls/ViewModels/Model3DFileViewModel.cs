using BG3PakViewer.Extensions;
using BG3PakViewer.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HelixToolkit.Wpf.SharpDX;
using LSLib.Granny.Model;
using Serilog;

namespace BG3PakViewer.Controls.ViewModels;

public partial class Model3DFileViewModel(IMessenger messenger) : ObservableObject
{
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    [ObservableProperty] public partial Root? Data { get; set; }

    // ReSharper disable once MemberCanBeMadeStatic.Global
    [ObservableProperty] public partial IEnumerable<string>? Meshes { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    // ReSharper disable once MemberCanBeMadeStatic.Global
    public partial int SelectedModelIndex { get; set; } = -1;

    [ObservableProperty] public partial ObservableElement3DCollection Models { get; set; } = [];

    partial void OnDataChanged(Root? value)
    {
        Log.Information("Model3DFileViewModel.DataChanged");
        if (value?.Meshes == null)
        {
            SelectedModelIndex = -1;
            Log.Information("Model3DFileViewModel.DataChanged: No meshes");
            return;
        }

        Meshes = value.Meshes.Select(x => x.Name).ToArray();
        SelectedModelIndex = Meshes.Any() ? 0 : -1;
        Log.Information("Model3DFileViewModel.DataChanged: Meshes: {0}", Meshes.Count());
        Log.Information("Model3DFileViewModel.DataChanged: SelectedModelIndex: {0}", SelectedModelIndex);
    }

    private bool CanPreview()
    {
        return SelectedModelIndex != -1;
    }

    [RelayCommand(CanExecute = nameof(CanPreview))]
    private void Preview()
    {
        Models.Clear();
        Log.Information("Model3DFileViewModel.Preview");
        Models.Add(Data!.ToGeometry3D(SelectedModelIndex).ToGeometryModel3D());
        Log.Information("Model3DFileViewModel.Preview: Models: {0}", Models.Count);
    }

    [RelayCommand]
    private void Zoom()
    {
        messenger.Send(new ZoomExtentsMessage(), MessageTokens.ZoomExtents);
        Log.Information("Model3DFileViewModel.Zoom");
    }
}