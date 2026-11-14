using System.Runtime.InteropServices;
using System.Text;
using LSLib.LS;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.PageFiles;

/// <summary>
///     One open GTP page file: the stream it was read from and the chunk offsets needed to read its
///     tiles.
/// </summary>
/// <remarks>
///     Everything the reader relies on is validated before it is used: the page size the tile set declares,
///     the page count, each page's offset table and each chunk's position and size. A malformed or
///     truncated page file therefore fails with an <see cref="InvalidDataException" /> at the first read
///     instead of allocating from, or reading past, whatever the corrupt data claims.
/// </remarks>
internal sealed class VirtualTextureTilePage : IAsyncDisposable
{
    /// <summary>
    ///     The largest page size accepted from the tile set that owns the page files. It is a sanity bound,
    ///     not a format limit: every BG3 tile set uses 1 MiB pages, so this leaves 4x headroom, and it also
    ///     bounds the offset table of one page (at most one uint per four bytes of page).
    /// </summary>
    private const int MaxPageSize = 4 * 1024 * 1024;

    /// <summary>
    ///     The largest number of pages a page file may hold, so a corrupt page size cannot make the offset
    ///     table list grow without bound. BG3's largest page file holds 445 pages; this allows 4096, i.e.
    ///     4 GiB at the 1 MiB page size the game uses.
    /// </summary>
    private const int MaxPages = 4096;

    /// <summary>
    ///     The number of bytes read at a time when a page file that cannot seek is copied into memory.
    /// </summary>
    private const int CopyChunkSize = 64 * 1024;

    private static readonly int HeaderSize = Marshal.SizeOf<GTPHeader>();
    private static readonly int ChunkHeaderSize = Marshal.SizeOf<GTPChunkHeader>();

    private readonly List<uint[]> _chunkOffsets;
    private readonly int _pageSize;
    private readonly BinaryReader _reader;

    private bool _disposed;

    /// <summary>
    ///     Opens a page file and reads the chunk offset table of every one of its pages, so chunks can be
    ///     addressed by page and chunk index afterward.
    /// </summary>
    /// <param name="pageSize">The aligned size of one page in the page file, in bytes.</param>
    /// <param name="stream">
    ///     The content of the page file; the page file keeps reading from it and closes it, or closes the copy
    ///     it makes when the stream cannot seek.
    /// </param>
    /// <exception cref="InvalidDataException">The page file is not a readable GTP page file.</exception>
    public VirtualTextureTilePage(int pageSize, Stream stream)
    {
        try
        {
            ValidatePageSize(pageSize);
            _pageSize = pageSize;
            stream = CopyToMemoryIfNeeded(pageSize, stream);
            _reader = new BinaryReader(stream, Encoding.UTF8, false);
            _chunkOffsets = ReadChunkOffsetTables();
        }
        catch
        {
            // The page file owns the stream from the moment it is handed over, so a rejected file must not
            // leave it open. Once the reader exists it closes that same stream.
            if (_reader is null)
                stream.Dispose();
            else
                _reader.Dispose();
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;
        _reader.Dispose();
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Positions the stream at the chunk (<paramref name="pageIndex" />, <paramref name="chunkIndex" />)
    ///     and returns its GTP chunk header together with the raw (still compressed) chunk bytes.
    /// </summary>
    /// <param name="pageIndex">The page within this page file.</param>
    /// <param name="chunkIndex">The chunk within that page.</param>
    /// <returns>The chunk header and the bytes it describes, still compressed.</returns>
    /// <exception cref="InvalidDataException">The page or chunk index, or the chunk itself, is out of range.</exception>
    public (GTPChunkHeader Header, byte[] Data) ReadChunk(int pageIndex, int chunkIndex)
    {
        var offset = GetChunkOffset(pageIndex, chunkIndex);

        _reader.BaseStream.Position = offset + (long)pageIndex * _pageSize;
        var header = BinUtils.ReadStruct<GTPChunkHeader>(_reader);
        ValidateChunkEnd(pageIndex, chunkIndex, offset, header.Size);

        return (header, ReadChunkData(pageIndex, chunkIndex, header.Size));
    }

    /// <summary>
    ///     Rejects a page size the page file cannot be read with.
    /// </summary>
    /// <param name="pageSize">The page size the tile set declares.</param>
    /// <exception cref="InvalidDataException">The page size is not within the supported range.</exception>
    private static void ValidatePageSize(int pageSize)
    {
        if (pageSize is <= 0 or > MaxPageSize)
            throw new InvalidDataException(
                $"Page size {pageSize} is outside the supported range 1..{MaxPageSize}");
    }

    /// <summary>
    ///     Makes the page file seekable, copying a stream that cannot seek into memory first.
    /// </summary>
    /// <param name="pageSize">The aligned size of one page in the page file, in bytes.</param>
    /// <param name="stream">The stream of the page file, which is closed when it has to be copied.</param>
    /// <returns>The stream to read the page file from.</returns>
    /// <exception cref="InvalidDataException">The stream is longer than a page file this reader accepts.</exception>
    private static Stream CopyToMemoryIfNeeded(int pageSize, Stream stream)
    {
        if (stream.CanSeek) return stream;

        var buffer = new MemoryStream();
        try
        {
            CopyToMemory(pageSize, stream, buffer);
        }
        catch
        {
            buffer.Dispose();
            throw;
        }

        stream.Dispose();
        // The copy has to be read from its start, the way the original stream was: the writes above
        // leave the buffer at its end, and the header is the first thing the offset tables are read
        // from.
        buffer.Position = 0;
        return buffer;
    }

    /// <summary>
    ///     Copies the page file into memory, stopping at the largest page file the reader accepts: a longer
    ///     stream would be rejected for holding more pages than <see cref="MaxPages" /> anyway.
    /// </summary>
    /// <param name="pageSize">The aligned size of one page in the page file, in bytes.</param>
    /// <param name="source">The stream to copy.</param>
    /// <param name="buffer">The buffer the page file is copied into.</param>
    /// <exception cref="InvalidDataException">The stream is longer than the largest page file.</exception>
    private static void CopyToMemory(int pageSize, Stream source, Stream buffer)
    {
        var maxLength = (long)MaxPages * pageSize;
        var chunk = new byte[CopyChunkSize];
        int read;
        while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > maxLength)
                throw new InvalidDataException(
                    $"Page file is longer than the largest one of {MaxPages} pages ({maxLength} bytes)");

            buffer.Write(chunk, 0, read);
        }
    }

    /// <summary>
    ///     Reads the chunk offset table of every page: each page stores its own count of offsets
    ///     followed by that many uint offsets, and pages are aligned to the page size.
    /// </summary>
    /// <exception cref="InvalidDataException">The header, the page layout or an offset table is corrupt.</exception>
    private List<uint[]> ReadChunkOffsetTables()
    {
        ValidateLayout();

        // The GTP header carries no offset this reader needs, so it is only stepped over.
        BinUtils.ReadStruct<GTPHeader>(_reader);

        var numPages = GetPageCount();
        var tables = new List<uint[]>(numPages);
        for (var page = 0; page < numPages; page++) tables.Add(ReadPageOffsets(page));

        return tables;
    }

    /// <summary>
    ///     Checks that the stream is long enough for the GTP header and is a whole number of pages.
    /// </summary>
    /// <exception cref="InvalidDataException">The stream is shorter than the header or not page aligned.</exception>
    private void ValidateLayout()
    {
        var length = _reader.BaseStream.Length;
        if (length < HeaderSize)
            throw new InvalidDataException(
                $"Page file is {length} bytes, shorter than its {HeaderSize}-byte GTP header");
        if (length % _pageSize != 0)
            throw new InvalidDataException(
                $"Page file is {length} bytes, not a whole number of {_pageSize}-byte pages");
    }

    /// <summary>
    ///     Gets the number of pages in the stream, rejecting a count the reader will not hold.
    /// </summary>
    /// <returns>The number of pages in the page file.</returns>
    /// <exception cref="InvalidDataException">The page file holds more pages than supported.</exception>
    private int GetPageCount()
    {
        var numPages = (int)(_reader.BaseStream.Length / _pageSize);
        if (numPages > MaxPages)
            throw new InvalidDataException(
                $"Page file holds {numPages} pages, more than the supported {MaxPages}");

        return numPages;
    }

    /// <summary>
    ///     Reads the chunk offsets of one page, which start right after the stream position and must fit
    ///     before the page ends.
    /// </summary>
    /// <param name="page">The page to read the offsets of.</param>
    /// <returns>The chunk offsets of that page, relative to the page start.</returns>
    /// <exception cref="InvalidDataException">The offset table does not fit in the page.</exception>
    private uint[] ReadPageOffsets(int page)
    {
        var pageEnd = (long)(page + 1) * _pageSize;
        var numOffsets = ReadOffsetCount(page, pageEnd);

        var offsets = new uint[numOffsets];
        BinUtils.ReadStructs(_reader, offsets);
        _reader.BaseStream.Position = pageEnd;

        return offsets;
    }

    /// <summary>
    ///     Reads the number of chunk offsets one page declares after checking that its table fits in the
    ///     page.
    /// </summary>
    /// <param name="page">The page being read.</param>
    /// <param name="pageEnd">The end of that page in the stream.</param>
    /// <returns>The number of offsets the page declares.</returns>
    /// <exception cref="InvalidDataException">The offset table does not fit in the page.</exception>
    private uint ReadOffsetCount(int page, long pageEnd)
    {
        if (_reader.BaseStream.Position + sizeof(uint) > pageEnd)
            throw new InvalidDataException($"Page {page} has no room for its chunk offset count");

        var numOffsets = _reader.ReadUInt32();
        if (_reader.BaseStream.Position + (long)numOffsets * sizeof(uint) > pageEnd)
            throw new InvalidDataException(
                $"Page {page} declares {numOffsets} chunk offsets, which do not fit in a {_pageSize}-byte page");

        return numOffsets;
    }

    /// <summary>
    ///     Gets the offset of a chunk after checking the indexes and that its header fits in the page.
    /// </summary>
    /// <param name="pageIndex">The page within this page file.</param>
    /// <param name="chunkIndex">The chunk within that page.</param>
    /// <returns>The chunk offset, relative to the page start.</returns>
    /// <exception cref="InvalidDataException">The page or chunk index, or the chunk offset, is out of range.</exception>
    private uint GetChunkOffset(int pageIndex, int chunkIndex)
    {
        var offsets = GetPageOffsets(pageIndex);
        if ((uint)chunkIndex >= (uint)offsets.Length)
            throw new InvalidDataException(
                $"Chunk index {chunkIndex} is outside the {offsets.Length} chunks of page {pageIndex}");

        var offset = offsets[chunkIndex];
        ValidateChunkOffset(pageIndex, chunkIndex, offset);

        return offset;
    }

    /// <summary>
    ///     Gets the offset table of one page after checking the page index.
    /// </summary>
    /// <param name="pageIndex">The page within this page file.</param>
    /// <returns>The chunk offsets of that page.</returns>
    /// <exception cref="InvalidDataException">The page index is outside the page file.</exception>
    private uint[] GetPageOffsets(int pageIndex)
    {
        if ((uint)pageIndex >= (uint)_chunkOffsets.Count)
            throw new InvalidDataException(
                $"Page index {pageIndex} is outside the {_chunkOffsets.Count} pages of the page file");

        return _chunkOffsets[pageIndex];
    }

    /// <summary>
    ///     Checks that the chunk header at the given offset still fits in its page.
    /// </summary>
    /// <param name="pageIndex">The page within this page file.</param>
    /// <param name="chunkIndex">The chunk within that page.</param>
    /// <param name="offset">The chunk offset, relative to the page start.</param>
    /// <exception cref="InvalidDataException">The chunk offset is past the page end.</exception>
    private void ValidateChunkOffset(int pageIndex, int chunkIndex, uint offset)
    {
        if (offset + ChunkHeaderSize > _pageSize)
            throw new InvalidDataException(
                $"Chunk {chunkIndex} of page {pageIndex} starts at offset {offset}, past its {_pageSize}-byte page");
    }

    /// <summary>
    ///     Checks that the data a chunk header declares still ends inside the chunk's page.
    /// </summary>
    /// <param name="pageIndex">The page within this page file.</param>
    /// <param name="chunkIndex">The chunk within that page.</param>
    /// <param name="offset">The chunk offset, relative to the page start.</param>
    /// <param name="size">The chunk size the header declares.</param>
    /// <exception cref="InvalidDataException">The chunk data does not fit in the page.</exception>
    private void ValidateChunkEnd(int pageIndex, int chunkIndex, uint offset, uint size)
    {
        if (offset + ChunkHeaderSize + size > _pageSize)
            throw new InvalidDataException(
                $"Chunk {chunkIndex} of page {pageIndex} declares {size} bytes, past its {_pageSize}-byte page");
    }

    /// <summary>
    ///     Reads the still compressed bytes of a chunk and rejects a short read.
    /// </summary>
    /// <param name="pageIndex">The page within this page file.</param>
    /// <param name="chunkIndex">The chunk within that page.</param>
    /// <param name="size">The chunk size the header declares.</param>
    /// <returns>The chunk bytes.</returns>
    /// <exception cref="InvalidDataException">Fewer bytes were available than the header declares.</exception>
    private byte[] ReadChunkData(int pageIndex, int chunkIndex, uint size)
    {
        var data = _reader.ReadBytes((int)size);
        if (data.Length != (int)size)
            throw new InvalidDataException(
                $"Chunk {chunkIndex} of page {pageIndex} is truncated: expected {size} bytes, read {data.Length}");

        return data;
    }
}
