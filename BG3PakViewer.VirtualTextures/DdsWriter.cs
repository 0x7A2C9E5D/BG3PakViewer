using System.Text;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

public sealed class DdsWriter
{
    private readonly Stream _output;

    public DdsWriter(Stream output, int width, int height)
    {
        this._output = output;

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

        using var bw = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        BinUtils.WriteStruct(bw, ref header);
    }

    public void WriteStrip(byte[] strip, int length) => _output.Write(strip, 0, length);
}