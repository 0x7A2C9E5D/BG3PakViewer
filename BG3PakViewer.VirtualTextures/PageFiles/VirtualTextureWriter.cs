using System.Drawing;
using System.Text;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.PageFiles;

/// <summary>
///     Writes a single-mip DXT5 DDS: emits the header, then stitches one horizontal tile band into a
///     reusable strip buffer per row and writes its blocks.
/// </summary>
/// <remarks>
///     The writer owns neither the output stream nor the unpacker it was created with: the caller
///     supplies both and keeps them open for as long as this writer is used. The tile size it is given
///     includes the border every tile carries; the border is trimmed here, so every consumer of a tile
///     size uses the same convention.
/// </remarks>
internal sealed class VirtualTextureWriter
{
    private readonly Size _content;
    private readonly Size _grid;
    private readonly BC5Image _strip;
    private readonly int _tileBorder;
    private readonly VirtualTextureUnpacker _unpacker;
    private readonly BinaryWriter _writer;

    /// <summary>
    ///     Creates the strip buffer a band is stitched into and the writer the bands are emitted through;
    ///     nothing is written until <see cref="WriteHeader" /> runs.
    /// </summary>
    /// <param name="grid">How many tiles each band holds: the number of columns and rows.</param>
    /// <param name="tile">The pixel size of one tile, including its border.</param>
    /// <param name="tileBorder">The border, in pixels, that every tile carries on each side.</param>
    /// <param name="unpacker">The unpacker the tiles of a band are read through.</param>
    /// <param name="stream">The stream the DDS is written to; the caller keeps it opens.</param>
    public VirtualTextureWriter(Size grid, Size tile, int tileBorder, VirtualTextureUnpacker unpacker,
        Stream stream)
    {
        _grid = grid;
        _content = new Size(tile.Width - tileBorder * 2, tile.Height - tileBorder * 2);
        _strip = new BC5Image(grid.Width * _content.Width, _content.Height);
        _tileBorder = tileBorder;
        _unpacker = unpacker;
        _writer = new BinaryWriter(stream, Encoding.UTF8, true);
    }

    /// <summary>
    ///     The number of bytes one band occupies in the DDS; the bands follow each other in the stream.
    /// </summary>
    public int RowByteCount => _strip.Data.Length;

    /// <summary>
    ///     Writes the DDS header of the image the bands form, which the bands follow in the stream.
    /// </summary>
    public Task WriteHeader()
    {
        // The numeric values are the DDS bit fields: 0x1007 is DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH |
        // DDSD_PIXELFORMAT, 0x04 in dwPFFlags is DDPF_FOURCC and 0x1000 in dwCaps is DDSCAPS_TEXTURE.
        var header = new DDSHeader
        {
            dwMagic = DDSHeader.DDSMagic,
            dwSize = DDSHeader.HeaderSize,
            dwFlags = 0x1007,
            dwWidth = (uint)(_grid.Width * _content.Width),
            dwHeight = (uint)(_grid.Height * _content.Height),
            dwPitchOrLinearSize = (uint)(_grid.Width * _content.Width * _grid.Height * _content.Height),
            dwDepth = 1,
            dwMipMapCount = 1,
            dwPFSize = 32,
            dwPFFlags = 0x04,
            dwFourCC = DDSHeader.FourCC_DXT5,
            dwCaps = 0x1000
        };
        BinUtils.WriteStruct(_writer, ref header);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Writes one band of tiles, leaving the columns without a tile transparent.
    /// </summary>
    /// <param name="tiles">The tiles of the band, in column order; a missing tile is written as empty.</param>
    /// <returns>A task that completes when the band has been written.</returns>
    public async Task WriteRow(Memory<GTSFlatTileInfo?> tiles)
    {
        // The strip is reused by every band, so a column without a tile is cleared instead of keeping the
        // pixels of the band above it.
        Array.Clear(_strip.Data);
        for (var col = 0; col < tiles.Length; col++)
        {
            if (tiles.Span[col] is not { } cell) continue;
            (await _unpacker.Decompress(cell)).CopyTileTo(_strip, _content, _tileBorder, col);
        }

        _writer.Write(_strip.Data, 0, _strip.Data.Length);
    }
}
