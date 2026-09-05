using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;

namespace BG3PakViewer.Shared.Extensions;

/// <summary>
///     GeometryModel3DExtensions
/// </summary>
public static class GeometryModel3DExtensions
{
    /// <summary>
    ///     Convert geometry to geometry model 3d.
    /// </summary>
    /// <param name="geometry"></param>
    /// <returns></returns>
    public static MeshGeometryModel3D ToGeometryModel3D(this Geometry3D geometry)
    {
        return new MeshGeometryModel3D
        {
            Geometry = geometry,
            Material = new DiffuseMaterial
            {
                DiffuseColor = Color4.White
            }
        };
    }
}