using BG3PakViewer.Extensions;
using BG3PakViewer.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HelixToolkit.Wpf.SharpDX;
using LSLib.Granny.Model;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing 3D models.
/// </summary>
public partial class Model3DPreviewViewModel : ObservableObject
{
    /// <summary>
    ///     The model to preview.
    /// </summary>
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    [ObservableProperty] public partial Root? Model { get; set; }

    /// <summary>
    ///     The list of meshes in the model.
    /// </summary>
    // ReSharper disable once MemberCanBeMadeStatic.Global
    [ObservableProperty] public partial string[]? Meshes { get; private set; }

    /// <summary>
    ///     The index of the currently selected mesh.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    // ReSharper disable once MemberCanBeMadeStatic.Global
    public partial int SelectedModelIndex { get; set; } = -1;

    /// <summary>
    ///     The 3D models to display in the viewport.
    /// </summary>
    [ObservableProperty] public partial ObservableElement3DCollection Models { get; set; } = [];

    /// <summary>
    ///     Resets the view model when the model is changed.
    /// </summary>
    /// <param name="value"></param>
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

    /// <summary>
    ///     Can preview if a model is selected.
    /// </summary>
    /// <returns></returns>
    private bool CanPreview()
    {
        return SelectedModelIndex != -1;
    }

    /// <summary>
    ///     Previews the selected model.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPreview))]
    private void Preview()
    {
        Models.Clear();
        Models.Add(Model!.ToGeometry3D(SelectedModelIndex).ToGeometryModel3D());
    }

    /// <summary>
    ///     Zooms to the model's extents.
    /// </summary>
    [RelayCommand]
    private static void Zoom()
    {
        WeakReferenceMessenger.Default.Send(string.Empty, MessageTokens.ZoomExtents);
    }
}