using System.Text;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

/// <summary>
///     TexturePage
/// </summary>
public sealed class TexturePage : IDisposable
{
    private readonly List<uint[]> _chunkOffsets;
    private readonly BinaryReader _reader;
    private readonly Stream _stream;
    private readonly VirtualTileSet _tileSet;

    /// <summary>
    ///     Constructs a new TexturePage from a stream.
    /// </summary>
    /// <param name="tileSet"></param>
    /// <param name="stream"></param>
    public TexturePage(VirtualTileSet tileSet, Stream stream)
    {
        _tileSet = tileSet;
        _stream = stream;
        _reader = new BinaryReader(_stream, Encoding.UTF8, false);

        BinUtils.ReadStruct<GTPHeader>(_reader);
        _chunkOffsets = ReadChunkOffsetTables();
    }

    public void Dispose()
    {
        _reader.Dispose();
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

    /// <summary>
    ///     Positions the stream at the chunk (<paramref name="pageIndex" />, <paramref name="chunkIndex" />)
    ///     and returns its GTP chunk header together with the raw (still compressed) chunk bytes.
    /// </summary>
    public (GTPChunkHeader Header, byte[] Data) ReadChunk(int pageIndex, int chunkIndex)
    {
        _stream.Position = _chunkOffsets[pageIndex][chunkIndex] + pageIndex * _tileSet.Header.PageSize;
        var header = BinUtils.ReadStruct<GTPChunkHeader>(_reader);
        return (header, _reader.ReadBytes((int)header.Size));
    }
}