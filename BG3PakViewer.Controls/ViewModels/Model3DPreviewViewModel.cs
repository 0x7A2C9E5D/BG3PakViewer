using BG3PakViewer.Controls.Extensions;
using BG3PakViewer.Shared.Messaging;
using BG3PakViewer.Shared.ViewModels;
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
public partial class Model3DPreviewViewModel : DisposableViewModel
{
    /// <summary>
    ///     Geometry cache for the loaded model's sub-meshes, keyed by mesh.
    /// </summary>
    private readonly Dictionary<Mesh, MeshGeometry3D> GeometryCache = [];

    public Model3DPreviewViewModel(Root modelRoot)
    {
        ModelRoot = modelRoot;
        LoadMeshes(modelRoot);
        UpdateSelectedMeshGeometry();
    }

    /// <summary>
    ///     The root of the loaded model to preview.
    /// </summary>
    [ObservableProperty]
    // ReSharper disable once PropertyCanBeMadeInitOnly.Local
    // ReSharper disable once MemberCanBeMadeStatic.Local
    private partial Root ModelRoot { get; set; }

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
    // ReSharper disable once MemberCanBeMadeStatic.Global
    public partial Mesh? SelectedMesh { get; set; }

    /// <summary>
    ///     The selected sub-mesh model displayed in the viewport.
    /// </summary>
    [ObservableProperty]
    public partial MeshGeometryModel3D? SelectedMeshGeometry { get; set; }

    /// <summary>
    ///     Loads the model root's meshes and selects the first one.
    /// </summary>
    /// <param name="value"></param>
    private void LoadMeshes(Root value)
    {
        if (value.Meshes == null)
        {
            Meshes = null;
            SelectedMesh = null;
            return;
        }

        Meshes = [.. value.Meshes.OrderBy(x => x.Name, StringComparer.InvariantCulture)];
        SelectedMesh = Meshes.FirstOrDefault();
    }

    /// <summary>
    ///     Updates the selected sub-mesh model when the selected mesh changes.
    /// </summary>
    /// <param name="value"></param>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedMeshChanged(Mesh? value)
    {
        UpdateSelectedMeshGeometry();
    }

    /// <summary>
    ///     Gets cached geometry for a mesh, building it on first use.
    /// </summary>
    private MeshGeometryModel3D GetOrCreateGeometry(Mesh mesh)
    {
        if (GeometryCache.TryGetValue(mesh, out var geometry))
            return geometry.ToGeometryModel3D();
        var root = ModelRoot;
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

    /// <summary>
    ///     Detaches the viewport model before it is released with the view model.
    /// </summary>
    /// <param name="disposing">Whether managed resources are released.</param>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        var geometry = SelectedMeshGeometry;
        SelectedMeshGeometry = null;
        geometry?.Dispose();
        GeometryCache.Clear();
    }
}
