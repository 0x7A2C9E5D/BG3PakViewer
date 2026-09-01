using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

/// <summary>
///     Decompresses individual tiles from GTP page files and stitches one horizontal band
///     of tiles into a reusable strip buffer, trimming tile borders.
/// </summary>
internal sealed class DdsTileUnpacker(VirtualTileSet tileSet, PageFileCache pageFileCache)
{
    private readonly TileCompressor _compressor = new();

    private int TileWidth => tileSet.Header.TileWidth - tileSet.Header.TileBorder * 2;

    private int TileHeight => tileSet.Header.TileHeight - tileSet.Header.TileBorder * 2;

    /// <summary>Stitches one horizontal band of tiles into <paramref name="strip" />, trimming tile borders.</summary>
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
    ///     Decompresses the tile at (<paramref name="x" />, <paramref name="y" />) of the current level/layer,
    ///     or returns null when that position has no tile.
    /// </summary>
    private BC5Image? TryUnpackTile(int level, int layer, int x, int y, ref GTSFlatTileInfo tileInfo)
    {
        if (!tileSet.GetTileInfo(level, layer, x, y, ref tileInfo)) return null;
        var pageFile = pageFileCache.Get(tileInfo.PageFileIndex);
        return UnpackChunkBc5(pageFile, tileInfo.PageIndex, tileInfo.ChunkIndex);
    }

    /// <summary>Decompresses the chunk at (<paramref name="pageIndex" />, <paramref name="chunkIndex" />) into a BC5 image.</summary>
    private BC5Image UnpackChunkBc5(StreamingPageFile pageFile, int pageIndex, int chunkIndex)
    {
        var header = tileSet.Header;
        var outputSize = 16 * ((header.TileWidth + 3) / 4) * ((header.TileHeight + 3) / 4)
                         + 16 * ((header.TileWidth / 2 + 3) / 4) * ((header.TileHeight / 2 + 3) / 4);
        return new BC5Image(UnpackChunk(pageFile, pageIndex, chunkIndex, outputSize), header.TileWidth,
            header.TileHeight);
    }

    private byte[] UnpackChunk(StreamingPageFile pageFile, int pageIndex, int chunkIndex, int outputSize)
    {
        var (chunkHeader, compressed) = pageFile.ReadChunk(pageIndex, chunkIndex);
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return chunkHeader.Codec switch
        {
            GTSCodec.Uniform => new byte[tileSet.Header.TileWidth * tileSet.Header.TileHeight],
            GTSCodec.BC => DecompressBc(chunkHeader, compressed, outputSize),
            _ => throw new InvalidDataException($"Unsupported codec: {chunkHeader.Codec}")
        };
    }

    private byte[] DecompressBc(GTPChunkHeader chunkHeader, byte[] compressed, int outputSize)
    {
        var parameterBlock = (GTSBCParameterBlock)tileSet.ParameterBlocks[chunkHeader.ParameterBlockID];
        return _compressor.Decompress(compressed, outputSize, parameterBlock.CompressionName1,
            parameterBlock.CompressionName2);
    }
}