using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Decompresses single GTP chunks into BC5 tiles.
/// </summary>
/// <remarks>
///     A chunk is addressed by the page and the chunk index it occupies inside its page file. It is either
///     uniform, carrying the single RGBA8 texel the whole tile repeats, or BC, compressed with the
///     parameter block its header names. The decoder knows nothing about which page file a chunk lives in:
///     the page arrives as a <see cref="VirtualTexturePage" />.
/// </remarks>
/// <param name="tileWidth">The pixel width of one tile, without its border.</param>
/// <param name="tileHeight">The pixel height of one tile, without its border.</param>
/// <param name="parameterBlocks">The codec parameter blocks of the tile set, by parameter block id.</param>
/// <param name="compressor">The decompressor the chunks are unpacked with.</param>
internal sealed class VirtualTexturePageChunkDecoder(
    int tileWidth,
    int tileHeight,
    IReadOnlyDictionary<uint, object> parameterBlocks,
    TileCompressor compressor)
{
    /// <summary>
    ///     Decompresses the chunk at (<paramref name="pageIndex" />, <paramref name="chunkIndex" />) into
    ///     a tile image sized as the tile declares.
    /// </summary>
    /// <param name="page">The page file the chunk lives in.</param>
    /// <param name="pageIndex">The page within that page file.</param>
    /// <param name="chunkIndex">The chunk within that page.</param>
    /// <returns>The decompressed tile, trimmed to the tile size without its border.</returns>
    /// <exception cref="InvalidDataException">The chunk uses a codec other than uniform or BC.</exception>
    public BC5Image ReadTile(VirtualTexturePage page, int pageIndex, int chunkIndex)
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