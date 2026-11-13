using System.Drawing;
using System.Text;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Writes a single-mip DXT5 DDS: emits the header, then stitches one horizontal tile band into a
///     reusable strip buffer per row and writes its blocks.
/// </summary>
/// <remarks>
///     The writer owns neither the output stream nor its writer: the caller supplies the stream or the
///     writer it writes through and keeps it open, so creating the writer has no side effect.
/// </remarks>
internal sealed class VirtualTextureWriter
{
    private readonly Size _grid;
    private readonly BC5Image _strip;
    private readonly Size _tile;
    private readonly int _tileBorder;
    private readonly VirtualTextureUnpacker _unpacker;
    private readonly BinaryWriter _writer;

    /// <summary>
    ///     Constructs a new VirtualTextureWriter from an output stream, creating the writer it writes
    ///     through.
    /// </summary>
    /// <param name="grid">How many tiles each band holds: the number of columns and rows.</param>
    /// <param name="tile">The pixel size of one tile, without its border.</param>
    /// <param name="tileBorder">The border, in pixels, that every tile carries on each side.</param>
    /// <param name="unpacker">The unpacker the tiles of a band are read through.</param>
    /// <param name="stream">The stream the DDS is written to; the caller keeps it opens.</param>
    public VirtualTextureWriter(Size grid, Size tile, int tileBorder, VirtualTextureUnpacker unpacker,
        Stream stream)
        : this(grid, tile, tileBorder, unpacker, new BinaryWriter(stream, Encoding.UTF8, true))
    {
    }

    /// <summary>
    ///     Constructs a new VirtualTextureWriter from a writer the caller created.
    /// </summary>
    /// <param name="grid">How many tiles each band holds: the number of columns and rows.</param>
    /// <param name="tile">The pixel size of one tile, without its border.</param>
    /// <param name="tileBorder">The border, in pixels, that every tile carries on each side.</param>
    /// <param name="unpacker">The unpacker the tiles of a band are read through.</param>
    /// <param name="writer">The writer the DDS is written through; the caller owns it.</param>
    private VirtualTextureWriter(Size grid, Size tile, int tileBorder, VirtualTextureUnpacker unpacker,
        BinaryWriter writer)
    {
        _grid = grid;
        _tile = tile;
        _tileBorder = tileBorder;
        _unpacker = unpacker;
        _writer = writer;
        _strip = new BC5Image(_grid.Width * _tile.Width, _tile.Height);
    }

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
            dwWidth = (uint)(_grid.Width * _tile.Width),
            dwHeight = (uint)(_grid.Height * _tile.Height),
            dwPitchOrLinearSize = (uint)(_grid.Width * _tile.Width * _grid.Height * _tile.Height),
            dwDepth = 1,
            dwMipMapCount = 1,
            dwPFSize = 32,
            dwPFFlags = 0x04,
            dwFourCC = DDSHeader.FourCC_DXT5,
            dwCaps = 0x1000
        };
        BinUtils.WriteStruct(_writer, ref header);
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
            _unpacker.ReadTile(tile)
                .CopyTo(_strip, _tileBorder, _tileBorder, col * _tile.Width, 0, _tile.Width, _tile.Height);
        }

        _writer.Write(_strip.Data, 0, _strip.Data.Length);
    }
}