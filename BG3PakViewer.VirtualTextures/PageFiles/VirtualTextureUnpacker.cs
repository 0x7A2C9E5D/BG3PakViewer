using System.Drawing;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.PageFiles;

/// <summary>
///     Decompresses single tiles from the GTP page files.
/// </summary>
/// <remarks>
///     The unpacker keeps at most one-page file open: the <see cref="VirtualTexturePageFileCache" /> it reads through
///     closes the open page file when the next tile lives in another one, so the number of open GTP
///     streams never grows with the size of the tile set. It knows nothing about tile sets: a tile
///     arrives as the chunk coordinates it occupies inside the page files, and how to reach those page
///     files is an argument.
/// </remarks>
/// <param name="pageSize">The aligned size of one page in the page files, in bytes.</param>
/// <param name="tile">The pixel size of one tile, including its border.</param>
/// <param name="pageStreamProvider">Opens the page file at the given index, e.g. from inside a PAK.</param>
/// <param name="parameterBlocks">The codec parameter blocks of the tile set, by parameter block id.</param>
/// <param name="compressor">The decompressor the chunks are unpacked with.</param>
internal sealed class VirtualTextureUnpacker(
    int pageSize,
    Size tile,
    Func<int, Stream> pageStreamProvider,
    IReadOnlyDictionary<uint, object> parameterBlocks,
    TileCompressor compressor)
    : IDisposable
{
    private readonly VirtualTexturePageChunkDecoder _decoder = new(tile, parameterBlocks, compressor);

    private readonly VirtualTexturePageFileCache _virtualTexturePages = new(pageSize, pageStreamProvider);

    /// <summary>
    ///     Closes the page file that is still open.
    /// </summary>
    public void Dispose()
    {
        _virtualTexturePages.Dispose();
    }

    /// <summary>
    ///     Decompresses the tile the given chunk coordinates point at.
    /// </summary>
    /// <param name="tile">The page file, page and chunk the tile occupies.</param>
    /// <returns>The decompressed tile, trimmed to the tile size without its border.</returns>
    public BC5Image ReadTile(in GTSFlatTileInfo tile)
    {
        var page = _virtualTexturePages.GetPage(tile.PageFileIndex);
        return _decoder.ReadTile(page, tile.PageIndex, tile.ChunkIndex);
    }
}