using HelixToolkit;
using LSLib.Granny.Model;

namespace BG3PakViewer.Extensions;

public static class TriTopologyExtensions
{
    public static IntCollection ToIntCollection(this TriTopology triTopology)
    {
        return [.. triTopology.Indices ?? triTopology.Indices16.Select(Convert.ToInt32)];
    }
}