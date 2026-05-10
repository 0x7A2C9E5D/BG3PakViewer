using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using LSLib.Granny.Model;

namespace BG3PakViewer.Extensions;

public static class MeshGeometryExtensions
{
    public static MeshGeometry3D ToGeometry3D(this Root root, int number)
    {
        var mesh = root.Meshes[number];
        var vertices = root.VertexDatas[number].Vertices;
        return new MeshGeometry3D
        {
            Indices = mesh.PrimaryTopology.ToIntCollection(),
            Positions = [.. vertices.Select(x => x.Position.ToVector3())],
            Normals = [.. vertices.Select(x => x.Normal.ToVector3())],
            BiTangents = [.. vertices.Select(x => x.Binormal.ToVector3())],
            Tangents = [.. vertices.Select(x => x.Tangent.ToVector3())],
            TextureCoordinates = [.. vertices.Select(x => x.TextureCoordinates0.ToVector2())],
            Colors = [.. vertices.Select(x => x.Color0.ToVector4().ToColor4())]
        };
    }
}