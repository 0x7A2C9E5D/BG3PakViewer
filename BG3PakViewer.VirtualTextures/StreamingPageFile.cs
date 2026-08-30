using System.Text;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

public sealed class StreamingPageFile : IDisposable
{
    private readonly VirtualTileSet _tileSet;
    private readonly Stream _stream;
    private readonly BinaryReader _reader;
    private readonly List<uint[]> _chunkOffsets;

    public StreamingPageFile(VirtualTileSet tileSet, Stream stream)
    {
        _tileSet = tileSet;
        _stream = stream;
        _reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        BinUtils.ReadStruct<GTPHeader>(_reader);

        var pageSize = tileSet.Header.PageSize;
        var numPages = (int)(stream.Length / pageSize);
        _chunkOffsets = new List<uint[]>(numPages);
        for (var page = 0; page < numPages; page++)
        {
            var numOffsets = _reader.ReadUInt32();
            var offsets = new uint[numOffsets];
            BinUtils.ReadStructs(_reader, offsets);
            _chunkOffsets.Add(offsets);
            stream.Position = (page + 1) * pageSize;
        }
    }

    private byte[] UnpackTile(int pageIndex, int chunkIndex, int outputSize, TileCompressor compressor)
    {
        _stream.Position = _chunkOffsets[pageIndex][chunkIndex] + pageIndex * _tileSet.Header.PageSize;
        var header = BinUtils.ReadStruct<GTPChunkHeader>(_reader);
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return header.Codec switch
        {
            GTSCodec.Uniform => DoUnpackTileUniform(),
            GTSCodec.BC => DoUnpackTileBc(header, outputSize, compressor),
            _ => throw new InvalidDataException($"Unsupported codec: {header.Codec}")
        };
    }

    public BC5Image UnpackTileBc5(int pageIndex, int chunkIndex, TileCompressor compressor)
    {
        var hdr = _tileSet.Header;
        var outputSize = 16 * ((hdr.TileWidth + 3) / 4) * ((hdr.TileHeight + 3) / 4)
                       + 16 * ((hdr.TileWidth / 2 + 3) / 4) * ((hdr.TileHeight / 2 + 3) / 4);
        return new BC5Image(UnpackTile(pageIndex, chunkIndex, outputSize, compressor), hdr.TileWidth, hdr.TileHeight);
    }

    private byte[] DoUnpackTileBc(GTPChunkHeader header, int outputSize, TileCompressor compressor)
    {
        var parameterBlock = (GTSBCParameterBlock)_tileSet.ParameterBlocks[header.ParameterBlockID];
        var compressed = _reader.ReadBytes((int)header.Size);
        return compressor.Decompress(compressed, outputSize, parameterBlock.CompressionName1, parameterBlock.CompressionName2);
    }

    private byte[] DoUnpackTileUniform()
    {
        return new byte[_tileSet.Header.TileWidth * _tileSet.Header.TileHeight];
    }

    public void Dispose()
    {
        _reader.Dispose();
    }
}
