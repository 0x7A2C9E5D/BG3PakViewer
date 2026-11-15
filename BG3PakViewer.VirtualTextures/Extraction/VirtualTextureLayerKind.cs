namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     How a virtual texture layer is meant to be shown in a preview, derived from the data type the
///     tile set declares for the layer.
/// </summary>
public enum VirtualTextureLayerKind
{
    /// <summary>
    ///     A color layer (sRGB or linear RGB, with or without alpha). Its alpha channel carries real
    ///     transparency where the source texture has one; the game renders such layers with the
    ///     material's own transparency handling, and fully transparent layers are shown opaque.
    /// </summary>
    Color,

    /// <summary>
    ///     A data layer (normals and packed material parameters). Its channels are material inputs the
    ///     shader combines, so the preview shows the raw RGB channels and never applies transparency.
    /// </summary>
    Data
}
