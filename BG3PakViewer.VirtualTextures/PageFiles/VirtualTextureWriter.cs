using System.Drawing;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.PageFiles;

/// <summary>
///     Writes a single-mip DXT5 DDS: emits the header, then stitches one horizontal tile band into a
///     reusable strip buffer per row and writes its blocks.
/// </summary>
/// <remarks>
///     The writer keeps the output and the unpacker it was created with and owns neither of them: the
///     caller supplies both and keeps them open for as long as this writer is used.
/// </remarks>
/// <param name="grid">How many tiles each band holds: the number of columns and rows.</param>
/// <param name="tile">The pixel size of one tile, without its border.</param>
/// <param name="tileBorder">The border, in pixels, that every tile carries on each side.</param>
/// <param name="unpacker">The unpacker the tiles of a band are read through.</param>
/// <param name="writer">The writer the DDS is written through; the caller owns it.</param>
internal sealed class VirtualTextureWriter(
    Size grid,
    Size tile,
    int tileBorder,
    VirtualTextureUnpacker unpacker,
    BinaryWriter writer)
{
    private readonly BC5Image _strip = new(grid.Width * tile.Width, tile.Height);

    /// <summary>
    ///     Writes the DDS header of the image the bands form, which the bands follow in the stream.
    /// </summary>
    public void WriteHeader()
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
    ///     Writes one band of tiles, leaving the columns without a tile transparent.
    /// </summary>
    /// <param name="tiles">The tiles of the band, in column order; a missing tile is written as empty.</param>
    /// <param name="ct">Cancels to write.</param>
    public void WriteRow(ReadOnlySpan<GTSFlatTileInfo?> tiles, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Array.Clear(_strip.Data);
        for (var col = 0; col < tiles.Length; col++)
        {
            if (tiles[col] is not { } cell) continue;
            unpacker.ReadTile(cell).CopyTo(_strip, tile, tileBorder, col);
        }

        writer.Write(_strip.Data, 0, _strip.Data.Length);
    }
}
