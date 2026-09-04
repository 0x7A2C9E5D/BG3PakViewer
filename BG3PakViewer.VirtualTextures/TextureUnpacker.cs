using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

/// <summary>
///     Decompresses individual tiles from GTP page files and stitches one horizontal band
///     of tiles into a reusable strip buffer, trimming tile borders.
/// </summary>
public sealed class TextureUnpacker(VirtualTileSet tileSet, TexturePageCache texturePageCache)
{
    private readonly TileCompressor _compressor = new();

    private int TileWidth => tileSet.Header.TileWidth - tileSet.Header.TileBorder * 2;

    private int TileHeight => tileSet.Header.TileHeight - tileSet.Header.TileBorder * 2;

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
        Array.Clear(strip.Data);
        GTSFlatTileInfo tileInfo = default;
        for (var col = 0; col < cols; col++)
        {
            var tile = TryUnpackTile(level, layer, startX + col, y, ref tileInfo);
            // Skip the tile border and stitch into the strip row band via LSLib's BC5Image.CopyTo (4x4 blocks)
            tile?.CopyTo(strip, tileSet.Header.TileBorder, tileSet.Header.TileBorder,
                col * TileWidth, 0, TileWidth, TileHeight);
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
            GTSCodec.Uniform => new byte[tileSet.Header.TileWidth * tileSet.Header.TileHeight],
            GTSCodec.BC => DecompressBc(chunkHeader, compressed, outputSize),
            _ => throw new InvalidDataException($"Unsupported codec: {chunkHeader.Codec}")
        };
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