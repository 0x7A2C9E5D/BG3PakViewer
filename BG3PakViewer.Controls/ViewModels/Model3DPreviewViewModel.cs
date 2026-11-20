using BG3PakViewer.Messaging;
using BG3PakViewer.Shared.Extensions;
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
    [ObservableProperty]
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    public partial Root? Model { get; set; }

    /// <summary>
    ///     The list of meshes in the model, sorted by name for display.
    /// </summary>
    // ReSharper disable once MemberCanBeMadeStatic.Global
    [ObservableProperty]
    public partial Mesh[]? Meshes { get; private set; }

    /// <summary>
    ///     The currently selected mesh.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    // ReSharper disable once MemberCanBeMadeStatic.Global
    public partial Mesh? SelectedMesh { get; set; }

    /// <summary>
    ///     The 3D models to display in the viewport.
    /// </summary>
    [ObservableProperty]
    public partial ObservableElement3DCollection Models { get; set; } = [];

    /// <summary>
    ///     Resets the view model when the model is changed.
    /// </summary>
    /// <param name="value"></param>
    partial void OnModelChanged(Root? value)
    {
        if (value?.Meshes == null)
        {
            Meshes = null;
            SelectedMesh = null;
            return;
        }

        Meshes = [.. value.Meshes.OrderBy(x => x.Name, StringComparer.InvariantCulture)];
        SelectedMesh = Meshes.FirstOrDefault();
    }

    /// <summary>
    ///     Can preview if a mesh is selected.
    /// </summary>
    /// <returns></returns>
    private bool CanPreview()
    {
        return SelectedMesh != null;
    }

    /// <summary>
    ///     Previews the selected model.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPreview))]
    private void Preview()
    {
        var root = Model!;
        var index = root.Meshes!.IndexOf(SelectedMesh!);
        Models.Clear();
        Models.Add(root.ToGeometry3D(index).ToGeometryModel3D());
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
