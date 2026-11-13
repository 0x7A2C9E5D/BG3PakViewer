using System.Drawing;
using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.PageFiles;

/// <summary>
///     Decompresses single GTP chunks into BC5 tiles.
/// </summary>
/// <remarks>
///     A chunk is addressed by the page and the chunk index it occupies inside its page file. It is either
///     uniform, carrying the single RGBA8 texel the whole tile repeats, or BC, compressed with the
///     parameter block its header names. The decoder knows nothing about which page file a chunk lives in:
///     the page arrives as a <see cref="VirtualTextureTilePageFile" />.
/// </remarks>
/// <param name="tile">The pixel size of one tile, including its border.</param>
/// <param name="parameterBlocks">The codec parameter blocks of the tile set, by parameter block id.</param>
/// <param name="compressor">The decompressor the chunks are unpacked with.</param>
internal sealed class VirtualTextureTilePageChunkDecoder(
    Size tile,
    IReadOnlyDictionary<uint, object> parameterBlocks,
    TileCompressor compressor)
{
    /// <summary>
    ///     Decompresses the chunk at (<paramref name="pageIndex" />, <paramref name="chunkIndex" />) into
    ///     a tile image sized as the tile declares.
    /// </summary>
    /// <param name="tilePageFile">The page file the chunk lives in.</param>
    /// <param name="pageIndex">The page within that page file.</param>
    /// <param name="chunkIndex">The chunk within that page.</param>
    /// <returns>The decompressed tile, trimmed to the tile size without its border.</returns>
    /// <exception cref="InvalidDataException">The chunk uses a codec other than uniform or BC.</exception>
    public BC5Image ReadTile(VirtualTextureTilePageFile tilePageFile, int pageIndex, int chunkIndex)
    {
        // BC5 stores two channels: sixteen bytes per 4x4 block for the one kept at full resolution, and
        // the same again for the one kept at half resolution.
        var outputSize = 16 * ((tile.Width + 3) / 4) * ((tile.Height + 3) / 4)
                         + 16 * ((tile.Width / 2 + 3) / 4) * ((tile.Height / 2 + 3) / 4);
        return new BC5Image(UnpackChunk(tilePageFile, pageIndex, chunkIndex, outputSize), tile.Width, tile.Height);
    }

    /// <summary>
    ///     Decompresses a chunk and returns its block data.
    /// </summary>
    /// <param name="tilePageFile">The page file the chunk lives in.</param>
    /// <param name="pageIndex">The page within that page file.</param>
    /// <param name="chunkIndex">The chunk within that page.</param>
    /// <param name="outputSize">The number of decompressed bytes to produce.</param>
    /// <returns>The decompressed block data.</returns>
    /// <exception cref="InvalidDataException">The chunk uses a codec other than uniform or BC.</exception>
    private byte[] UnpackChunk(VirtualTextureTilePageFile tilePageFile, int pageIndex, int chunkIndex, int outputSize)
    {
        var (chunkHeader, compressed) = tilePageFile.ReadChunk(pageIndex, chunkIndex);
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return chunkHeader.Codec switch
        {
            GTSCodec.Uniform => CreateUniformTile(compressed),
            GTSCodec.BC => DecompressChunk(chunkHeader, compressed, outputSize),
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
        var block = EncodeConstantColor(payload);
        var blockCount = (tile.Width + 3) / 4 * ((tile.Height + 3) / 4);
        var data = new byte[blockCount * 16];
        for (var i = 0; i < blockCount; i++) block.CopyTo(data, i * 16);
        return data;
    }

    /// <summary>
    ///     Encodes one color as a BC3 (DXT5) block in which every texel resolves to that color.
    ///     This mirrors LSLib, which runs sixteen identical pixels through the BCnEncoder block encoder
    ///     instead of quantizing the endpoints by hand, so uniform tiles come out byte for byte alike.
    /// </summary>
    /// <param name="payload">
    ///     The RGBA8 texel every texel of the block resolves to, of which only the first four bytes are read.
    /// </param>
    /// <returns>One BC3 block whose sixteen texels all resolve to that color.</returns>
    private static byte[] EncodeConstantColor(byte[] payload)
    {
        var source = new ColorRgba32[16];
        Array.Fill(source, payload.ToColorRgba32());
        return new BcEncoder(CompressionFormat.Bc3).EncodeBlock(source);
    }

    /// <summary>
    ///     Decompresses a BC chunk.
    /// </summary>
    /// <param name="chunkHeader">The chunk header naming the parameter block that describes the codec.</param>
    /// <param name="compressed">The still-compressed chunk bytes.</param>
    /// <param name="outputSize">The number of decompressed bytes to produce.</param>
    /// <returns>The decompressed block data.</returns>
    private byte[] DecompressChunk(GTPChunkHeader chunkHeader, byte[] compressed, int outputSize)
    {
        var parameterBlock = (GTSBCParameterBlock)parameterBlocks[chunkHeader.ParameterBlockID];
        return compressor.Decompress(compressed, outputSize, parameterBlock.CompressionName1,
            parameterBlock.CompressionName2);
    }
}