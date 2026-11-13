using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Decompresses individual tiles from GTP page files and stitches one horizontal band
///     of tiles into a reusable strip buffer, trimming tile borders.
/// </summary>
internal sealed class VirtualTextureUnpacker(VirtualTileSet tileSet, VirtualTexturePageCache pageCache)
{
    private readonly TileCompressor _compressor = new();

    /// <summary>
    ///     Decompresses and stitches one horizontal band of tiles into a reusable strip buffer.
    /// </summary>
    /// <param name="level">The index of the mip level the tiles are read from.</param>
    /// <param name="layer">The index of the layer the tiles are read from.</param>
    /// <param name="startX">The tile column the band starts at.</param>
    /// <param name="y">The tile row the band is read from.</param>
    /// <param name="cols">How many tiles the band holds.</param>
    /// <param name="strip">
    ///     The strip buffer the band is stitched into; it is cleared first and ends up holding the BC3
    ///     blocks of the whole band.
    /// </param>
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
    /// <param name="level">The index of the mip level to read.</param>
    /// <param name="layer">The index of the layer to read.</param>
    /// <param name="x">The tile column.</param>
    /// <param name="y">The tile row.</param>
    /// <param name="tileInfo">
    ///     The tile info the tile set fills in; reused across calls so a band does not allocate one per tile.
    /// </param>
    /// <returns>The decompressed tile, or <see langword="null" /> when the tile set has no tile there.</returns>
    private BC5Image? TryUnpackTile(int level, int layer, int x, int y, ref GTSFlatTileInfo tileInfo)
    {
        if (!tileSet.GetTileInfo(level, layer, x, y, ref tileInfo)) return null;
        var pageFile = pageCache.Get(tileInfo.PageFileIndex);
        return UnpackChunkBc5(pageFile, tileInfo.PageIndex, tileInfo.ChunkIndex);
    }

    /// <summary>
    ///     Decompresses a chunk into a tile image sized as the tile header declares.
    /// </summary>
    /// <param name="page">The page file the chunk lives in.</param>
    /// <param name="pageIndex">The page within that page file.</param>
    /// <param name="chunkIndex">The chunk within that page.</param>
    /// <returns>The decompressed tile.</returns>
    private BC5Image UnpackChunkBc5(VirtualTexturePage page, int pageIndex, int chunkIndex)
    {
        var header = tileSet.Header;
        var outputSize = 16 * ((header.TileWidth + 3) / 4) * ((header.TileHeight + 3) / 4)
                         + 16 * ((header.TileWidth / 2 + 3) / 4) * ((header.TileHeight / 2 + 3) / 4);
        return new BC5Image(UnpackChunk(page, pageIndex, chunkIndex, outputSize), header.TileWidth,
            header.TileHeight);
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
        var blockCount = (tileSet.Header.TileWidth + 3) / 4 * ((tileSet.Header.TileHeight + 3) / 4);
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
        var parameterBlock = (GTSBCParameterBlock)tileSet.ParameterBlocks[chunkHeader.ParameterBlockID];
        return _compressor.Decompress(compressed, outputSize, parameterBlock.CompressionName1,
            parameterBlock.CompressionName2);
    }
}