using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Decompresses single tiles from the GTP page files.
/// </summary>
/// <remarks>
///     The unpacker holds at most one-page file open: when the next tile lives in another page file, the
///     open one is closed before the new one is opened, so the number of open GTP streams never grows
///     with the size of the tile set. It knows nothing about tile sets: a tile arrives as the chunk
///     coordinates it occupies inside the page files, and how to reach those page files is an argument.
/// </remarks>
/// <param name="pageSize">The aligned size of one page in the page files, in bytes.</param>
/// <param name="tileWidth">The pixel width of one tile, without its border.</param>
/// <param name="tileHeight">The pixel height of one tile, without its border.</param>
/// <param name="pageStreamProvider">Opens the page file at the given index, e.g. from inside a PAK.</param>
/// <param name="parameterBlocks">The codec parameter blocks of the tile set, by parameter block id.</param>
/// <param name="compressor">The decompressor the chunks are unpacked with.</param>
internal sealed class VirtualTextureUnpacker(
    int pageSize,
    int tileWidth,
    int tileHeight,
    Func<int, Stream> pageStreamProvider,
    IReadOnlyDictionary<uint, object> parameterBlocks,
    TileCompressor compressor)
    : IDisposable
{
    private int _openPageFileIndex = -1;
    private VirtualTexturePage? _page;

    /// <summary>
    ///     Closes the page file that is still open.
    /// </summary>
    public void Dispose()
    {
        ClosePage();
    }

    /// <summary>
    ///     Decompresses the tile the given chunk coordinates point at.
    /// </summary>
    /// <param name="tile">The page file, page and chunk the tile occupies.</param>
    /// <returns>The decompressed tile, trimmed to the tile size without its border.</returns>
    public BC5Image ReadTile(in GTSFlatTileInfo tile)
    {
        var page = GetPage(tile.PageFileIndex);
        return UnpackChunkBc5(page, tile.PageIndex, tile.ChunkIndex);
    }

    /// <summary>
    ///     Gets the page file at <paramref name="pageFileIndex" />, closing the one that is open when it
    ///     is a different file.
    /// </summary>
    /// <param name="pageFileIndex">The index of the page file in the tile set's page file list.</param>
    /// <returns>The page file, ready to read chunks from.</returns>
    private VirtualTexturePage GetPage(int pageFileIndex)
    {
        if (_page != null && _openPageFileIndex == pageFileIndex) return _page;

        ClosePage();

        var stream = pageStreamProvider(pageFileIndex);
        if (!stream.CanSeek)
        {
            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            stream.Dispose();
            stream = buffer;
        }

        _page = new VirtualTexturePage(pageSize, stream);
        _openPageFileIndex = pageFileIndex;
        return _page;
    }

    /// <summary>
    ///     Closes the page file that is open, if any.
    /// </summary>
    private void ClosePage()
    {
        _page?.Dispose();
        _page = null;
        _openPageFileIndex = -1;
    }

    /// <summary>
    ///     Decompresses a chunk into a tile image sized as the tile declares.
    /// </summary>
    /// <param name="page">The page file the chunk lives in.</param>
    /// <param name="pageIndex">The page within that page file.</param>
    /// <param name="chunkIndex">The chunk within that page.</param>
    /// <returns>The decompressed tile.</returns>
    private BC5Image UnpackChunkBc5(VirtualTexturePage page, int pageIndex, int chunkIndex)
    {
        var outputSize = 16 * ((tileWidth + 3) / 4) * ((tileHeight + 3) / 4)
                         + 16 * ((tileWidth / 2 + 3) / 4) * ((tileHeight / 2 + 3) / 4);
        return new BC5Image(UnpackChunk(page, pageIndex, chunkIndex, outputSize), tileWidth, tileHeight);
    }

    /// <summary>
    ///     Decompresses a chunk and returns its block data.
    /// </summary>
    /// <param name="page">The page file the chunk lives in.</param>
    /// <param name="pageIndex">The page within that page file.</param>
    /// <param name="chunkIndex">The chunk within that page.</param>
    /// <param name="outputSize">The number of decompressed bytes to produce.</param>
    /// <returns>The decompressed block data.</returns>
    /// <exception cref="InvalidDataException">The chunk uses a codec other than uniform or BC.</exception>
    private byte[] UnpackChunk(VirtualTexturePage page, int pageIndex, int chunkIndex, int outputSize)
    {
        var (chunkHeader, compressed) = page.ReadChunk(pageIndex, chunkIndex);
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return chunkHeader.Codec switch
        {
            GTSCodec.Uniform => CreateUniformTile(compressed),
            GTSCodec.BC => DecompressBc(chunkHeader, compressed, outputSize),
            _ => throw new InvalidDataException($"Unsupported codec: {chunkHeader.Codec}")
        };
    }

    /// <summary>
    ///     Expands a uniform chunk into the BC3 blocks covering a whole tile. The payload of such a
    ///     chunk is the tile's single RGBA8 texel (the uniform parameter block declares a 4x1 image),
    ///     so the tile is one flat color and every block repeats it.
    /// </summary>
    /// <param name="payload">The uniform chunk payload: the single RGBA8 texel the chunk declares.</param>
    /// <returns>The BC3 blocks of one tile, every block being that color.</returns>
    /// <exception cref="InvalidDataException">The payload is shorter than one RGBA8 texel.</exception>
    private byte[] CreateUniformTile(byte[] payload)
    {
        if (payload.Length < 4)
            throw new InvalidDataException(
                $"Uniform chunk payload is {payload.Length} bytes, expected one RGBA8 texel");

        var block = EncodeConstantColor(payload[0], payload[1], payload[2], payload[3]);
        var blockCount = (tileWidth + 3) / 4 * ((tileHeight + 3) / 4);
        var data = new byte[blockCount * 16];
        for (var i = 0; i < blockCount; i++) block.CopyTo(data, i * 16);
        return data;
    }

    /// <summary>
    ///     Encodes one color as a BC3 (DXT5) block in which every texel resolves to that color.
    ///     This mirrors LSLib, which runs sixteen identical pixels through the BCnEncoder block encoder
    ///     instead of quantizing the endpoints by hand, so uniform tiles come out byte for byte alike.
    /// </summary>
    /// <param name="r">The red channel, 0-255.</param>
    /// <param name="g">The green channel, 0-255.</param>
    /// <param name="b">The blue channel, 0-255.</param>
    /// <param name="a">The alpha channel, 0-255.</param>
    /// <returns>One BC3 block whose sixteen texels all resolve to that color.</returns>
    private static byte[] EncodeConstantColor(byte r, byte g, byte b, byte a)
    {
        var source = new ColorRgba32[16];
        Array.Fill(source, new ColorRgba32(r, g, b, a));
        return new BcEncoder(CompressionFormat.Bc3).EncodeBlock(source);
    }

    /// <summary>
    ///     Decompresses a BC chunk.
    /// </summary>
    /// <param name="chunkHeader">The chunk header naming the parameter block that describes the codec.</param>
    /// <param name="compressed">The still-compressed chunk bytes.</param>
    /// <param name="outputSize">The number of decompressed bytes to produce.</param>
    /// <returns>The decompressed block data.</returns>
    private byte[] DecompressBc(GTPChunkHeader chunkHeader, byte[] compressed, int outputSize)
    {
        var parameterBlock = (GTSBCParameterBlock)parameterBlocks[chunkHeader.ParameterBlockID];
        return compressor.Decompress(compressed, outputSize, parameterBlock.CompressionName1,
            parameterBlock.CompressionName2);
    }
}
