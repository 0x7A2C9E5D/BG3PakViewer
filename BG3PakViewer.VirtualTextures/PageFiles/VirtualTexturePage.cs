using System.Text;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.PageFiles;

/// <summary>
///     One open GTP page file: the stream it was read from and the chunk offsets needed to read its
///     tiles.
/// </summary>
internal sealed class VirtualTexturePage : IDisposable
{
    private readonly List<uint[]> _chunkOffsets;
    private readonly int _pageSize;
    private readonly BinaryReader _reader;

    /// <summary>
    ///     Opens a page file and reads the chunk offset table of every one of its pages, so chunks can be
    ///     addressed by page and chunk index afterwards.
    /// </summary>
    /// <param name="pageSize">The aligned size of one page in the page file, in bytes.</param>
    /// <param name="stream">The content of the page file; the page keeps reading from it.</param>
    public VirtualTexturePage(int pageSize, Stream stream)
    {
        _pageSize = pageSize;
        _reader = new BinaryReader(stream, Encoding.UTF8, false);
        _chunkOffsets = ReadChunkOffsetTables();
    }

    /// <summary>
    ///     Closes the reader, which closes the page file stream as well.
    /// </summary>
    public void Dispose()
    {
        _reader.Dispose();
    }

    /// <summary>
    ///     Reads the chunk offset table of every page: each page stores its own count of offsets
    ///     followed by that many uint offsets, and pages are aligned to the page size.
    /// </summary>
    private List<uint[]> ReadChunkOffsetTables()
    {
        // The GTP header carries no offset this reader needs, so it is only stepped over.
        BinUtils.ReadStruct<GTPHeader>(_reader);
        var numPages = (int)(_reader.BaseStream.Length / _pageSize);
        var tables = new List<uint[]>(numPages);
        for (var page = 0; page < numPages; page++)
        {
            var numOffsets = _reader.ReadUInt32();
            var offsets = new uint[numOffsets];
            BinUtils.ReadStructs(_reader, offsets);
            tables.Add(offsets);
            _reader.BaseStream.Position = (page + 1) * _pageSize;
        }

        return tables;
    }

    /// <summary>
    ///     Positions the stream at the chunk (<paramref name="pageIndex" />, <paramref name="chunkIndex" />)
    ///     and returns its GTP chunk header together with the raw (still compressed) chunk bytes.
    /// </summary>
    /// <param name="pageIndex">The page within this page file.</param>
    /// <param name="chunkIndex">The chunk within that page.</param>
    /// <returns>The chunk header and the bytes it describes, still compressed.</returns>
    public (GTPChunkHeader Header, byte[] Data) ReadChunk(int pageIndex, int chunkIndex)
    {
        _reader.BaseStream.Position = _chunkOffsets[pageIndex][chunkIndex] + pageIndex * _pageSize;
        var header = BinUtils.ReadStruct<GTPChunkHeader>(_reader);
        return (header, _reader.ReadBytes((int)header.Size));
    }
}