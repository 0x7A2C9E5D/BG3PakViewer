using System.Numerics;

namespace BG3PakViewer.Extensions;

public static class VectorConversionExtensions
{
    public static Vector2 ToVector2(this OpenTK.Mathematics.Vector2 vector)
    {
        return new Vector2(vector.X, vector.Y);
    }

    public static Vector3 ToVector3(this OpenTK.Mathematics.Vector3 vector)
    {
        return new Vector3(vector.X, vector.Y, vector.Z);
    }

    public static Vector4 ToVector4(this OpenTK.Mathematics.Vector4 vector)
    {
        return new Vector4(vector.X, vector.Y, vector.Z, vector.W);
    }
}