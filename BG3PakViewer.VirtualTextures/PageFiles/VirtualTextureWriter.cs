using System.Drawing;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.PageFiles;

/// <summary>
///     Writes a single-mip DXT5 DDS: emits the header, then stitches one horizontal tile band into a
///     strip buffer per row and writes its blocks.
/// </summary>
/// <remarks>
///     The writer keeps no state at all: the output, the layout, the tiles and the strip buffer are
///     arguments of each call, so the same calls write any texture at any size to any output.
/// </remarks>
internal static class VirtualTextureWriter
{
    /// <summary>
    ///     Writes the DDS header of the image the bands form, which the bands follow in the stream.
    /// </summary>
    /// <param name="writer">The writer the DDS is written through; the caller owns it.</param>
    /// <param name="grid">How many tiles each band holds: the number of columns and rows.</param>
    /// <param name="tile">The pixel size of one tile, without its border.</param>
    public static void WriteHeader(BinaryWriter writer, Size grid, Size tile)
    {
        var header = new DDSHeader
        {
            dwMagic = DDSHeader.DDSMagic,
            dwSize = DDSHeader.HeaderSize,
            dwFlags = 0x1007,
            dwWidth = (uint)(grid.Width * tile.Width),
            dwHeight = (uint)(grid.Height * tile.Height),
            dwPitchOrLinearSize = (uint)(grid.Width * tile.Width * grid.Height * tile.Height),
            dwDepth = 1,
            dwMipMapCount = 1,
            dwPFSize = 32,
            dwPFFlags = 0x04,
            dwFourCC = DDSHeader.FourCC_DXT5,
            dwCaps = 0x1000
        };
        BinUtils.WriteStruct(writer, ref header);
    }

    /// <summary>
    ///     Creates the strip buffer one band is stitched into, so the caller can reuse it for every band.
    /// </summary>
    /// <param name="grid">How many tiles each band holds: the number of columns and rows.</param>
    /// <param name="tile">The pixel size of one tile, without its border.</param>
    /// <returns>A buffer one band wide and one tile tall, to hand to every <see cref="WriteRow" />.</returns>
    public static BC5Image CreateStrip(Size grid, Size tile)
    {
        return new BC5Image(grid.Width * tile.Width, tile.Height);
    }

    /// <summary>
    ///     Writes one band of tiles, leaving the columns without a tile transparent.
    /// </summary>
    /// <param name="writer">The writer the DDS is written through; the caller owns it.</param>
    /// <param name="unpacker">The unpacker the tiles of the band are read through.</param>
    /// <param name="strip">The buffer the band is stitched into; it is cleared before every band.</param>
    /// <param name="tile">The pixel size of one tile, without its border.</param>
    /// <param name="tileBorder">The border, in pixels, that every tile carries on each side.</param>
    /// <param name="tiles">The tiles of the band, in column order; a missing tile is written as empty.</param>
    /// <param name="ct">Cancels to write.</param>
    public static void WriteRow(BinaryWriter writer, VirtualTextureUnpacker unpacker, BC5Image strip, Size tile,
        int tileBorder, ReadOnlySpan<GTSFlatTileInfo?> tiles, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Array.Clear(strip.Data);
        for (var col = 0; col < tiles.Length; col++)
        {
            if (tiles[col] is not { } cell) continue;
            unpacker.ReadTile(cell)
                .CopyTo(strip, tileBorder, tileBorder, col * tile.Width, 0, tile.Width, tile.Height);
        }

        writer.Write(strip.Data, 0, strip.Data.Length);
    }
}
