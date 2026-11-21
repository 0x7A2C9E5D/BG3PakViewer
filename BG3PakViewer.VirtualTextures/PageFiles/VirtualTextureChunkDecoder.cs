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
///     uniform, carrying the single RGBA8 texel the whole size repeats, or BC, compressed with the
///     parameter block its header names. The decoder knows nothing about which page file a chunk lives in:
///     the page arrives as a <see cref="VirtualTextureTilePage" />.
/// </remarks>
/// <param name="size">The pixel size of one size, including its border.</param>
/// <param name="parameterBlocks">The codec parameter blocks of the size set, by parameter block id.</param>
/// <param name="compressor">The decompressor the chunks are unpacked with.</param>
internal sealed class VirtualTextureChunkDecoder(
    IReadOnlyDictionary<uint, object> parameterBlocks,
    TileCompressor compressor,
    Size size)
{
    private readonly BcEncoder _encoder = new(CompressionFormat.Bc3);

    // BC5 stores two channels: sixteen bytes per 4x4 block for the one kept at full resolution, and the
    // same again for the one kept at half resolution.
    private readonly int _outputSize = 16 * ((size.Width + 3) / 4) * ((size.Height + 3) / 4)
                                       + 16 * ((size.Width / 2 + 3) / 4) * ((size.Height / 2 + 3) / 4);

    private readonly ColorRgba32[] _pixels = new ColorRgba32[16];

    // A uniform size is one flat color, so every one of its blocks is the same 16 bytes.
    private readonly int _uniformBlockCount = (size.Width + 3) / 4 * ((size.Height + 3) / 4);
    private BC5Image? _uniformTile;

    /// <summary>
    ///     Decompresses the chunk at (<paramref name="pageIndex" />, <paramref name="chunkIndex" />) into
    ///     a size image sized as the size declares.
    /// </summary>
    /// <param name="tilePage">The page file the chunk lives in.</param>
    /// <param name="pageIndex">The page within that page file.</param>
    /// <param name="chunkIndex">The chunk within that page.</param>
    /// <returns>
    ///     The decompressed size, trimmed to the size without its border. A uniform chunk fills the
    ///     same image again on every call, so the caller has to copy it out before reading the next size.
    /// </returns>
    /// <exception cref="InvalidDataException">The chunk uses a codec other than uniform or BC.</exception>
    public BC5Image Decode(VirtualTextureTilePage tilePage, int pageIndex, int chunkIndex)
    {
        var (chunkHeader, compressed) = tilePage.ReadChunk(pageIndex, chunkIndex);
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return chunkHeader.Codec switch
        {
            GTSCodec.Uniform => CreateUniformTile(compressed),
            GTSCodec.BC => new BC5Image(DecompressChunk(chunkHeader, compressed), size.Width, size.Height),
            _ => throw new InvalidDataException($"Unsupported codec: {chunkHeader.Codec}")
        };
    }

    /// <summary>
    ///     Expands a uniform chunk into the BC3 blocks covering a whole size. The payload of such a
    ///     chunk is the size's single RGBA8 texel (the uniform parameter block declares a 4x1 image),
    ///     so the size is one flat color and every block repeats it.
    /// </summary>
    /// <param name="payload">The uniform chunk payload: the single RGBA8 texel the chunk declares.</param>
    /// <returns>The BC3 blocks of one size, every block being that color.</returns>
    /// <exception cref="InvalidDataException">The payload is shorter than one RGBA8 texel.</exception>
    private BC5Image CreateUniformTile(byte[] payload)
    {
        if (payload.Length < 4)
            throw new InvalidDataException(
                $"Uniform chunk payload is {payload.Length} bytes, expected one RGBA8 texel");
        var block = EncodeConstantColor(payload);
        var image = _uniformTile ??= new BC5Image(new byte[_uniformBlockCount * 16], size.Width, size.Height);
        for (var i = 0; i < _uniformBlockCount; i++) block.CopyTo(image.Data, i * 16);
        return image;
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
    private byte[] EncodeConstantColor(byte[] payload)
    {
        Array.Fill(_pixels, payload.ToColorRgba32());
        return _encoder.EncodeBlock(_pixels);
    }

    /// <summary>
    ///     Decompresses a BC chunk.
    /// </summary>
    /// <param name="chunkHeader">The chunk header naming the parameter block that describes the codec.</param>
    /// <param name="compressed">The still-compressed chunk bytes.</param>
    /// <returns>The decompressed block data.</returns>
    private byte[] DecompressChunk(GTPChunkHeader chunkHeader, byte[] compressed)
    {
        var parameterBlock = (GTSBCParameterBlock)parameterBlocks[chunkHeader.ParameterBlockID];
        return compressor.Decompress(compressed, _outputSize, parameterBlock.CompressionName1,
            parameterBlock.CompressionName2);
    }
}
