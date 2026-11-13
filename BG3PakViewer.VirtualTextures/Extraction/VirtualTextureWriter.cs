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
    private readonly int _cols;
    private readonly int _rows;
    private readonly BC5Image _strip;
    private readonly int _tileBorder;
    private readonly int _tileHeight;
    private readonly int _tileWidth;
    private readonly VirtualTextureUnpacker _unpacker;
    private readonly BinaryWriter _writer;

    /// <summary>
    ///     Constructs a new VirtualTextureWriter from an output stream, creating the writer it writes
    ///     through.
    /// </summary>
    /// <param name="cols">How many tiles each band holds.</param>
    /// <param name="rows">How many tile bands the texture spans.</param>
    /// <param name="tileWidth">The pixel width of one tile, without its border.</param>
    /// <param name="tileHeight">The pixel height of one tile, without its border.</param>
    /// <param name="tileBorder">The border, in pixels, that every tile carries on each side.</param>
    /// <param name="unpacker">The unpacker the tiles of a band are read through.</param>
    /// <param name="stream">The stream the DDS is written to; the caller keeps it open.</param>
    public VirtualTextureWriter(int cols, int rows, int tileWidth, int tileHeight, int tileBorder,
        VirtualTextureUnpacker unpacker, Stream stream)
        : this(cols, rows, tileWidth, tileHeight, tileBorder, unpacker,
            new BinaryWriter(stream, Encoding.UTF8, true))
    {
    }

    /// <summary>
    ///     Constructs a new VirtualTextureWriter from a writer the caller created.
    /// </summary>
    /// <param name="cols">How many tiles each band holds.</param>
    /// <param name="rows">How many tile bands the texture spans.</param>
    /// <param name="tileWidth">The pixel width of one tile, without its border.</param>
    /// <param name="tileHeight">The pixel height of one tile, without its border.</param>
    /// <param name="tileBorder">The border, in pixels, that every tile carries on each side.</param>
    /// <param name="unpacker">The unpacker the tiles of a band are read through.</param>
    /// <param name="writer">The writer the DDS is written through; the caller owns it.</param>
    private VirtualTextureWriter(int cols, int rows, int tileWidth, int tileHeight, int tileBorder,
        VirtualTextureUnpacker unpacker, BinaryWriter writer)
    {
        _cols = cols;
        _rows = rows;
        _tileBorder = tileBorder;
        _tileHeight = tileHeight;
        _tileWidth = tileWidth;
        _unpacker = unpacker;
        _writer = writer;
        _strip = new BC5Image(cols * tileWidth, tileHeight);
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
            dwWidth = (uint)(_cols * _tileWidth),
            dwHeight = (uint)(_rows * _tileHeight),
            dwPitchOrLinearSize = (uint)(_cols * _tileWidth * _rows * _tileHeight),
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
                .CopyTo(_strip, _tileBorder, _tileBorder, col * _tileWidth, 0, _tileWidth, _tileHeight);
        }

        _writer.Write(_strip.Data, 0, _strip.Data.Length);
    }
}
