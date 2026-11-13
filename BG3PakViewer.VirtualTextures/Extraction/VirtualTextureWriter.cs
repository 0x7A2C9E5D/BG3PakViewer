using System.Text;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Writes a single-mip DXT5 DDS to a stream: emits the header up front, then stitches one horizontal
///     tile band into a reusable strip buffer per row and writes its blocks.
/// </summary>
internal sealed class VirtualTextureWriter : IDisposable
{
    private readonly BinaryWriter _bw;
    private readonly BC5Image _strip;
    private readonly int _tileBorder;
    private readonly int _tileHeight;
    private readonly int _tileWidth;
    private readonly VirtualTextureUnpacker _unpacker;

    /// <summary>
    ///     Constructs a new VirtualTextureWriter.
    /// </summary>
    /// <param name="output">The stream the DDS bytes are written to; it stays open and the caller owns it.</param>
    /// <param name="cols">How many tiles each band holds.</param>
    /// <param name="rows">How many tile bands the texture spans.</param>
    /// <param name="tileWidth">The pixel width of one tile, without its border.</param>
    /// <param name="tileHeight">The pixel height of one tile, without its border.</param>
    /// <param name="tileBorder">The border, in pixels, that every tile carries on each side.</param>
    /// <param name="unpacker">The unpacker the tiles of a band are read through.</param>
    public VirtualTextureWriter(Stream output, int cols, int rows, int tileWidth, int tileHeight, int tileBorder,
        VirtualTextureUnpacker unpacker)
    {
        _bw = new BinaryWriter(output, Encoding.UTF8, true);
        WriteHeader(_bw, cols * tileWidth, rows * tileHeight);
        _strip = new BC5Image(cols * tileWidth, tileHeight);
        _tileWidth = tileWidth;
        _tileHeight = tileHeight;
        _tileBorder = tileBorder;
        _unpacker = unpacker;
    }

    /// <summary>
    ///     Closes the writer, which leaves the output stream open.
    /// </summary>
    public void Dispose()
    {
        _bw.Dispose();
    }

    /// <summary>
    ///     Writes one band of tiles to the output stream, leaving the columns without a tile transparent.
    /// </summary>
    /// <param name="tiles">The tiles of the band, in column order; a missing tile is written as empty.</param>
    /// <param name="ct">Cancels the write.</param>
    public void WriteRow(ReadOnlySpan<GTSFlatTileInfo?> tiles, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Array.Clear(_strip.Data);
        for (var col = 0; col < tiles.Length; col++)
        {
            if (tiles[col] is not { } tile) continue;
            _unpacker.ReadTile(tile).CopyTo(_strip, _tileBorder, _tileBorder, col * _tileWidth, 0, _tileWidth,
                _tileHeight);
        }

        _bw.Write(_strip.Data, 0, _strip.Data.Length);
    }

    /// <summary>
    ///     Writes a DDS header to the output stream.
    /// </summary>
    /// <param name="bw">The writer the header is written through.</param>
    /// <param name="width">The pixel width of the image the bands form.</param>
    /// <param name="height">The pixel height of the image the bands form.</param>
    private static void WriteHeader(BinaryWriter bw, int width, int height)
    {
        var header = new DDSHeader
        {
            dwMagic = DDSHeader.DDSMagic,
            dwSize = DDSHeader.HeaderSize,
            dwFlags = 0x1007,
            dwWidth = (uint)width,
            dwHeight = (uint)height,
            dwPitchOrLinearSize = (uint)(width * height),
            dwDepth = 1,
            dwMipMapCount = 1,
            dwPFSize = 32,
            dwPFFlags = 0x04,
            dwFourCC = DDSHeader.FourCC_DXT5,
            dwCaps = 0x1000
        };
        BinUtils.WriteStruct(bw, ref header);
    }
}
