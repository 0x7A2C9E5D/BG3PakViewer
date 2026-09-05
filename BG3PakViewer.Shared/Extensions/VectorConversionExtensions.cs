using System.Numerics;

namespace BG3PakViewer.Shared.Extensions;

/// <summary>
///     VectorConversionExtensions
/// </summary>
internal static class VectorConversionExtensions
{
    /// <summary>
    ///     Convert open t k vector 2 to vector 2.
    /// </summary>
    /// <param name="vector"></param>
    /// <returns></returns>
    public static Vector2 ToVector2(this OpenTK.Mathematics.Vector2 vector)
    {
        return new Vector2(vector.X, vector.Y);
    }

    /// <summary>
    ///     Convert open t k vector 3 to vector 3.
    /// </summary>
    /// <param name="vector"></param>
    /// <returns></returns>
    public static Vector3 ToVector3(this OpenTK.Mathematics.Vector3 vector)
    {
        return new Vector3(vector.X, vector.Y, vector.Z);
    }

    /// <summary>
    ///     Convert open t k vector 4 to vector 4.
    /// </summary>
    /// <param name="vector"></param>
    /// <returns></returns>
    public static Vector4 ToVector4(this OpenTK.Mathematics.Vector4 vector)
    {
        return new Vector4(vector.X, vector.Y, vector.Z, vector.W);
    }
}