using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;

namespace BG3PakViewer.Extensions;

public static class GeometryModel3DExtensions
{
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