using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

/// <summary>
///     Decompresses individual tiles from GTP page files and stitches one horizontal band
///     of tiles into a reusable strip buffer, trimming tile borders.
/// </summary>
public sealed class TextureUnpacker(VirtualTileSet tileSet, TexturePageCache texturePageCache)
{
    private readonly TileCompressor _compressor = new();

    /// <summary>
    ///     Decompresses and stitches one horizontal band of tiles into a reusable strip buffer.
    /// </summary>
    /// <param name="level"></param>
    /// <param name="layer"></param>
    /// <param name="startX"></param>
    /// <param name="y"></param>
    /// <param name="cols"></param>
    /// <param name="strip"></param>
    public void StitchRow(int level, int layer, int startX, int y, int cols, BC5Image strip)
    {
        var tileWidth = tileSet.EffectiveTileWidth;
        var tileHeight = tileSet.EffectiveTileHeight;
        Array.Clear(strip.Data);
        GTSFlatTileInfo tileInfo = default;
        for (var col = 0; col < cols; col++)
        {
            var tile = TryUnpackTile(level, layer, startX + col, y, ref tileInfo);
            // Skip the tile border and stitch into the strip row band via LSLib's BC5Image.CopyTo (4x4 blocks)
            tile?.CopyTo(strip, tileSet.Header.TileBorder, tileSet.Header.TileBorder,
                col * tileWidth, 0, tileWidth, tileHeight);
        }
    }

    /// <summary>
    ///     Tries to decompress and return a tile at (<paramref name="level" />, <paramref name="layer" />,
    ///     <paramref name="x" />, <paramref name="y" />).
    /// </summary>
    /// <param name="level"></param>
    /// <param name="layer"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="tileInfo"></param>
    /// <returns></returns>
    private BC5Image? TryUnpackTile(int level, int layer, int x, int y, ref GTSFlatTileInfo tileInfo)
    {
        if (!tileSet.GetTileInfo(level, layer, x, y, ref tileInfo)) return null;
        var pageFile = texturePageCache.Get(tileInfo.PageFileIndex);
        return UnpackChunkBc5(pageFile, tileInfo.PageIndex, tileInfo.ChunkIndex);
    }

    /// <summary>
    ///     Decompresses a chunk and returns the raw pixel data.
    /// </summary>
    /// <param name="page"></param>
    /// <param name="pageIndex"></param>
    /// <param name="chunkIndex"></param>
    /// <returns></returns>
    private BC5Image UnpackChunkBc5(TexturePage page, int pageIndex, int chunkIndex)
    {
        var header = tileSet.Header;
        var outputSize = 16 * ((header.TileWidth + 3) / 4) * ((header.TileHeight + 3) / 4)
                         + 16 * ((header.TileWidth / 2 + 3) / 4) * ((header.TileHeight / 2 + 3) / 4);
        return new BC5Image(UnpackChunk(page, pageIndex, chunkIndex, outputSize), header.TileWidth,
            header.TileHeight);
    }

    /// <summary>
    ///     Decompresses a chunk and returns the raw pixel data.
    /// </summary>
    /// <param name="page"></param>
    /// <param name="pageIndex"></param>
    /// <param name="chunkIndex"></param>
    /// <param name="outputSize"></param>
    /// <returns></returns>
    /// <exception cref="InvalidDataException"></exception>
    private byte[] UnpackChunk(TexturePage page, int pageIndex, int chunkIndex, int outputSize)
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
    ///     so the tile is one flat colour and every block repeats it.
    /// </summary>
    /// <param name="payload"></param>
    /// <returns></returns>
    /// <exception cref="InvalidDataException"></exception>
    private byte[] CreateUniformTile(byte[] payload)
    {
        if (payload.Length < 4)
            throw new InvalidDataException(
                $"Uniform chunk payload is {payload.Length} bytes, expected one RGBA8 texel");

        var block = EncodeConstantColor(payload[0], payload[1], payload[2], payload[3]);
        var blockCount = ((tileSet.Header.TileWidth + 3) / 4) * ((tileSet.Header.TileHeight + 3) / 4);
        var data = new byte[blockCount * 16];
        for (var i = 0; i < blockCount; i++) block.CopyTo(data, i * 16);
        return data;
    }

    /// <summary>
    ///     Encodes one colour as a BC3 (DXT5) block in which every texel resolves to that colour.
    ///     This mirrors LSLib, which runs sixteen identical pixels through the BCnEncoder block encoder
    ///     instead of quantizing the endpoints by hand, so uniform tiles come out byte for byte alike.
    /// </summary>
    /// <param name="r"></param>
    /// <param name="g"></param>
    /// <param name="b"></param>
    /// <param name="a"></param>
    /// <returns></returns>
    private static byte[] EncodeConstantColor(byte r, byte g, byte b, byte a)
    {
        var source = new ColorRgba32[16];
        Array.Fill(source, new ColorRgba32(r, g, b, a));
        return new BcEncoder(CompressionFormat.Bc3).EncodeBlock(source);
    }

    /// <summary>
    ///     Decompresses a BC chunk.
    /// </summary>
    /// <param name="chunkHeader"></param>
    /// <param name="compressed"></param>
    /// <param name="outputSize"></param>
    /// <returns></returns>
    private byte[] DecompressBc(GTPChunkHeader chunkHeader, byte[] compressed, int outputSize)
    {
        var parameterBlock = (GTSBCParameterBlock)tileSet.ParameterBlocks[chunkHeader.ParameterBlockID];
        return _compressor.Decompress(compressed, outputSize, parameterBlock.CompressionName1,
            parameterBlock.CompressionName2);
    }
}