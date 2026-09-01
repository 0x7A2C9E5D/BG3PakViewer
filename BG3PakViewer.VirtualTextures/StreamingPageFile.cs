using System.Text;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

public sealed class StreamingPageFile : IDisposable
{
    private readonly List<uint[]> _chunkOffsets;
    private readonly BinaryReader _reader;
    private readonly Stream _stream;
    private readonly VirtualTileSet _tileSet;

    public StreamingPageFile(VirtualTileSet tileSet, Stream stream)
    {
        _tileSet = tileSet;
        _stream = stream;
        _reader = new BinaryReader(_stream, Encoding.UTF8, false);

        BinUtils.ReadStruct<GTPHeader>(_reader);
        _chunkOffsets = ReadChunkOffsetTables();
    }

    /// <summary>
    ///     Reads the chunk offset table of every page: each page stores its own count of offsets
    ///     followed by that many uint offsets, and pages are aligned to VirtualTileSet.Header.PageSize.
    /// </summary>
    private List<uint[]> ReadChunkOffsetTables()
    {
        var pageSize = _tileSet.Header.PageSize;
        var numPages = (int)(_stream.Length / pageSize);
        var tables = new List<uint[]>(numPages);
        for (var page = 0; page < numPages; page++)
        {
            var numOffsets = _reader.ReadUInt32();
            var offsets = new uint[numOffsets];
            BinUtils.ReadStructs(_reader, offsets);
            tables.Add(offsets);
            _stream.Position = (page + 1) * pageSize;
        }
        return tables;
    }

    public void Dispose()
    {
        _reader.Dispose();
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
        var header = _tileSet.Header;
        var outputSize = 16 * ((header.TileWidth + 3) / 4) * ((header.TileHeight + 3) / 4)
                         + 16 * ((header.TileWidth / 2 + 3) / 4) * ((header.TileHeight / 2 + 3) / 4);
        return new BC5Image(UnpackTile(pageIndex, chunkIndex, outputSize, compressor), header.TileWidth,
            header.TileHeight);
    }

    private byte[] DoUnpackTileBc(GTPChunkHeader header, int outputSize, TileCompressor compressor)
    {
        var parameterBlock = (GTSBCParameterBlock)_tileSet.ParameterBlocks[header.ParameterBlockID];
        var compressed = _reader.ReadBytes((int)header.Size);
        return compressor.Decompress(compressed, outputSize, parameterBlock.CompressionName1,
            parameterBlock.CompressionName2);
    }

    private byte[] DoUnpackTileUniform()
    {
        return new byte[_tileSet.Header.TileWidth * _tileSet.Header.TileHeight];
    }
}