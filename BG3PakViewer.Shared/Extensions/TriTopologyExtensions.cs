using HelixToolkit;
using LSLib.Granny.Model;

namespace BG3PakViewer.Shared.Extensions;

/// <summary>
///     TriTopologyExtensions
/// </summary>
public static class TriTopologyExtensions
{
    /// <summary>
    ///     Convert tri topology to int collection.
    /// </summary>
    /// <param name="triTopology"></param>
    /// <returns></returns>
    public static IntCollection ToIntCollection(this TriTopology triTopology)
    {
        return [.. triTopology.Indices ?? triTopology.Indices16.Select(Convert.ToInt32)];
    }
}