using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.PageFiles;

/// <summary>
///     Decompresses single tiles from the GTP page files.
/// </summary>
/// <remarks>
///     It knows nothing about tile sets: a tile arrives as the chunk coordinates it occupies inside the
///     page files, and how to reach those page files is an argument. The chunks themselves are decoded by
///     the decoder the caller hands over, so this class only moves from a tile to the chunk data of a page
///     file.
/// </remarks>
/// <param name="pageSize">The aligned size of one page in the page files, in bytes.</param>
/// <param name="pageStreamProvider">Opens the page file at the given index, e.g. from inside a PAK.</param>
/// <param name="decoder">The decoder the chunks are decompressed with; the caller owns it.</param>
internal sealed class VirtualTextureUnpacker(
    int pageSize,
    Func<int, Stream> pageStreamProvider,
    VirtualTexturePageChunkDecoder decoder)
    : IDisposable
{
    private readonly VirtualTexturePageFileHolder _pageFiles = new(pageSize, pageStreamProvider);

    /// <summary>
    ///     Closes the page file that is still open.
    /// </summary>
    public void Dispose()
    {
        _pageFiles.Dispose();
    }

    /// <summary>
    ///     Decompresses the tile the given chunk coordinates point at.
    /// </summary>
    /// <param name="tile">The page file, page and chunk the tile occupies.</param>
    /// <returns>The decompressed tile, trimmed to the tile size without its border.</returns>
    public BC5Image ReadTile(in GTSFlatTileInfo tile)
    {
        var pageFile = _pageFiles.GetPageFile(tile.PageFileIndex);
        return decoder.ReadTile(pageFile, tile.PageIndex, tile.ChunkIndex);
    }
}