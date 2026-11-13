namespace BG3PakViewer.VirtualTextures.Models;

/// <summary>
///     The layers every virtual texture is previewed and exported as.
/// </summary>
/// <remarks>
///     Shared by the single tile set preview and the virtual texture toolbox so that both write the
///     same file names and agree on how many layers they support.
/// </remarks>
public static class VirtualTextureLayers
{
    /// <summary>
    ///     The layer names, in layer index order.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } = ["Albedo", "Normal", "Physical"];
}