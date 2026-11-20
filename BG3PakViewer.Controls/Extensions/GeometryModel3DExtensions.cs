using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;

namespace BG3PakViewer.Controls.Extensions;

/// <summary>
///     GeometryModel3DExtensions
/// </summary>
internal static class GeometryModel3DExtensions
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
