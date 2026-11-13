using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Writes a single-mip DXT5 DDS: emits the header, then stitches one horizontal tile band into a
///     reusable strip buffer per row and writes its blocks.
/// </summary>
/// <remarks>
///     The writer owns neither the output stream nor its writer: the caller supplies the
///     <see cref="BinaryWriter" /> every call writes through and keeps it open, so creating the writer has
///     no side effect.
/// </remarks>
/// <param name="cols">How many tiles each band holds.</param>
/// <param name="rows">How many tile bands the texture spans.</param>
/// <param name="tileWidth">The pixel width of one tile, without its border.</param>
/// <param name="tileHeight">The pixel height of one tile, without its border.</param>
/// <param name="tileBorder">The border, in pixels, that every tile carries on each side.</param>
/// <param name="unpacker">The unpacker the tiles of a band are read through.</param>
/// <param name="writer">The writer the DDS is written through; the caller owns it.</param>
internal sealed class VirtualTextureWriter(
    int cols,
    int rows,
    int tileWidth,
    int tileHeight,
    int tileBorder,
    VirtualTextureUnpacker unpacker,
    BinaryWriter writer)
{
    private readonly BC5Image _strip = new(cols * tileWidth, tileHeight);

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
            dwWidth = (uint)(cols * tileWidth),
            dwHeight = (uint)(rows * tileHeight),
            dwPitchOrLinearSize = (uint)(cols * tileWidth * rows * tileHeight),
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
            if (tiles[col] is not { } tile) continue;
            unpacker.ReadTile(tile).CopyTo(_strip, tileBorder, tileBorder, col * tileWidth, 0, tileWidth, tileHeight);
        }

        writer.Write(_strip.Data, 0, _strip.Data.Length);
    }
}