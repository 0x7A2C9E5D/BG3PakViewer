using BG3PakViewer.Messaging;
using BG3PakViewer.Shared.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using LSLib.Granny.Model;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing 3D models.
/// </summary>
public partial class Model3DPreviewViewModel : ObservableObject
{
    /// <summary>
    ///     Geometry cache for the loaded model's sub-meshes, keyed by mesh.
    /// </summary>
    private readonly Dictionary<Mesh, MeshGeometry3D> GeometryCache = [];

    private bool isPreviewing;

    /// <summary>
    ///     The root of the loaded model to preview.
    /// </summary>
    [ObservableProperty]
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    public partial Root? ModelRoot { get; set; }

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
    ///     The selected sub-mesh model displayed in the viewport.
    /// </summary>
    [ObservableProperty]
    public partial MeshGeometryModel3D? SelectedMeshGeometry { get; set; }

    /// <summary>
    ///     Resets the view model when the model root is changed.
    /// </summary>
    /// <param name="value"></param>
    partial void OnModelRootChanged(Root? value)
    {
        ResetPreviewState();
        LoadMeshes(value);
    }

    /// <summary>
    ///     Loads the model root's meshes and selects the first one.
    /// </summary>
    /// <param name="value"></param>
    private void LoadMeshes(Root? value)
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
    ///     Clears cached geometry and resets the preview state.
    /// </summary>
    private void ResetPreviewState()
    {
        isPreviewing = false;
        GeometryCache.Clear();
        SelectedMeshGeometry = null;
    }

    /// <summary>
    ///     Updates the selected sub-mesh model when the selected mesh changes after preview started.
    /// </summary>
    /// <param name="value"></param>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedMeshChanged(Mesh? value)
    {
        if (isPreviewing) UpdateSelectedMeshGeometry();
    }

    /// <summary>
    ///     Can preview if a mesh is selected and a model root is loaded.
    /// </summary>
    /// <returns></returns>
    private bool CanPreview()
    {
        return SelectedMesh != null && ModelRoot != null;
    }

    /// <summary>
    ///     Starts previewing the selected mesh, building its geometry on first use.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPreview))]
    private void Preview()
    {
        isPreviewing = true;
        UpdateSelectedMeshGeometry();
    }

    /// <summary>
    ///     Gets cached geometry for a mesh, building it on first use.
    /// </summary>
    private MeshGeometryModel3D GetOrCreateGeometry(Mesh mesh)
    {
        if (GeometryCache.TryGetValue(mesh, out var geometry))
            return geometry.ToGeometryModel3D();
        var root = ModelRoot!;
        var index = root.Meshes!.IndexOf(mesh);
        geometry = root.ToGeometry3D(index);
        GeometryCache[mesh] = geometry;
        return geometry.ToGeometryModel3D();
    }

    /// <summary>
    ///     Wraps the cached geometry of the selected mesh into the viewport model.
    /// </summary>
    private void UpdateSelectedMeshGeometry()
    {
        SelectedMeshGeometry = SelectedMesh is not null
            ? GetOrCreateGeometry(SelectedMesh)
            : null;
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
