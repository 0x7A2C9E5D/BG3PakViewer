using BCnEncoder.Shared;

namespace BG3PakViewer.VirtualTextures.PageFiles;

/// <summary>
///     Reads the texels the payloads of chunks carry.
/// </summary>
internal static class ByteArrayExtensions
{
    /// <summary>
    ///     Creates a color from the first four bytes of <paramref name="rgba" />, in the order the payload
    ///     of a uniform chunk stores them: red, green, blue, alpha.
    /// </summary>
    /// <param name="rgba">The RGBA8 texel, of which only the first four bytes are read.</param>
    /// <returns>The color of that texel.</returns>
    public static ColorRgba32 ToColorRgba32(this byte[] rgba)
    {
        return new ColorRgba32(rgba[0], rgba[1], rgba[2], rgba[3]);
    }
}
