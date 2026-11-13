using System.Text;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Writes a single-mip DXT5 DDS to a stream: emits the header up front, then stitches
///     one horizontal tile band into a reusable strip buffer per row, using the configured
///     tile unpacker for a fixed (level, layer) combination.
/// </summary>
internal sealed class VirtualTextureWriter : IDisposable
{
    private readonly BinaryWriter _bw;
    private readonly int _layer;
    private readonly int _level;
    private readonly BC5Image _strip;
    private readonly VirtualTextureUnpacker _unpacker;

    /// <summary>
    ///     Constructs a new VirtualTextureWriter.
    /// </summary>
    /// <param name="output">The stream the DDS bytes are written to; it stays open and the caller owns it.</param>
    /// <param name="cols">How many tiles each band holds.</param>
    /// <param name="rows">How many tile bands the texture spans.</param>
    /// <param name="tileWidth">The pixel width of one tile, without its border.</param>
    /// <param name="tileHeight">The pixel height of one tile, without its border.</param>
    /// <param name="unpacker">The unpacker that decompresses one band, bound to a fixed level and layer.</param>
    /// <param name="level">The index of the mip level the tiles are read from.</param>
    /// <param name="layer">The index of the layer to extract.</param>
    public VirtualTextureWriter(Stream output, int cols, int rows, int tileWidth, int tileHeight,
        VirtualTextureUnpacker unpacker, int level, int layer)
    {
        _bw = new BinaryWriter(output, Encoding.UTF8, true);
        WriteHeader(_bw, cols * tileWidth, rows * tileHeight);
        _strip = new BC5Image(cols * tileWidth, tileHeight);
        _unpacker = unpacker;
        _level = level;
        _layer = layer;
    }

    /// <summary>
    ///     Closes the writer, which leaves the output stream open.
    /// </summary>
    public void Dispose()
    {
        _bw.Dispose();
    }

    /// <summary>
    ///     Writes a single row of tiles to the output stream.
    /// </summary>
    /// <param name="startX">The tile column the band starts at.</param>
    /// <param name="y">The tile row the band is read from.</param>
    /// <param name="cols">How many tiles the band holds.</param>
    /// <param name="ct">Cancels the write.</param>
    public void WriteRow(int startX, int y, int cols, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _unpacker.StitchRow(_level, _layer, startX, y, cols, _strip);
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