using System.Text;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

/// <summary>
///     Writes a single-mip DXT5 DDS to a stream: emits the header up front, then stitches
///     one horizontal tile band into a reusable strip buffer per row. Row stitching is
///     delegated to a callback so the writer stays independent of the tile source.
/// </summary>
public sealed class TextureWriter : IDisposable
{
    private readonly BinaryWriter _bw;
    private readonly Action<int, int, int, BC5Image> _stitchRow;
    private readonly BC5Image _strip;

    public TextureWriter(Stream output, int cols, int rows, int tileWidth, int tileHeight,
        Action<int, int, int, BC5Image> stitchRow)
    {
        _bw = new BinaryWriter(output, Encoding.UTF8, true);
        WriteDdsHeader(_bw, cols * tileWidth, rows * tileHeight);
        _strip = new BC5Image(cols * tileWidth, tileHeight);
        _stitchRow = stitchRow;
    }

    public void Dispose()
    {
        _bw.Dispose();
    }

    /// <summary>Stitches and writes one horizontal band of tiles to the underlying stream.</summary>
    public void WriteRow(int startX, int y, int cols, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _stitchRow(startX, y, cols, _strip);
        _bw.Write(_strip.Data, 0, _strip.Data.Length);
    }

    /// <summary>Writes a single-mip DXT5 DDS header sized to the stitched output.</summary>
    private static void WriteDdsHeader(BinaryWriter bw, int width, int height)
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