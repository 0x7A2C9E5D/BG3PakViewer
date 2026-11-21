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
/// <param name="decoder">The decoder the chunks are decompressed with; the caller owns it.</param>
/// <param name="pageStreamProvider">Opens the page file at the given index, e.g. from inside a PAK.</param>
/// <param name="pageSize">The aligned size of one page in the page files, in bytes.</param>
internal sealed class VirtualTextureUnpacker(
    VirtualTextureChunkDecoder decoder,
    Func<int, Task<Stream>> pageStreamProvider,
    int pageSize)
    : IAsyncDisposable
{
    private readonly VirtualTextureTilePageSlot _tilePageFiles = new(pageSize, pageStreamProvider);
    private bool _disposed;

    /// <summary>
    ///     Closes the page file that is still open.
    /// </summary>
    /// <remarks>
    ///     Closing the reader is synchronous; <see cref="IAsyncDisposable" /> lets the extraction release
    ///     it with <c>await using</c> at the end of its asynchronous scope instead of a blocking disposal.
    /// </remarks>
    /// <returns>A task that completes when the page file is closed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _tilePageFiles.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }

    /// <summary>
    ///     Decompresses the tile the given chunk coordinates point at.
    /// </summary>
    /// <param name="tile">The page file, page and chunk the tile occupies.</param>
    /// <returns>The decompressed tile, trimmed to the tile size without its border.</returns>
    public async Task<BC5Image> Decompress(GTSFlatTileInfo tile)
    {
        var pageFile = await _tilePageFiles.GetPageFile(tile.PageFileIndex).ConfigureAwait(false);
        return decoder.Decode(pageFile, tile.PageIndex, tile.ChunkIndex);
    }
}
