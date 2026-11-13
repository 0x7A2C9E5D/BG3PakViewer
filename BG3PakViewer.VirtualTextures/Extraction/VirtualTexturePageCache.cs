using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Cache of open GTP page files, lazily opened per pageFileIndex: the provider is asked for the page
///     file name the tile set lists (e.g. read from inside a PAK), so no disk path is involved.
/// </summary>
internal sealed class VirtualTexturePageCache(VirtualTileSet tileSet, Func<string, Stream> streamProvider) : IDisposable
{
    private readonly Dictionary<int, VirtualTexturePage> _open = [];

    /// <summary>
    ///     Closes every page file that was opened so far.
    /// </summary>
    public void Dispose()
    {
        foreach (var file in _open.Values) file.Dispose();
        _open.Clear();
    }

    /// <summary>
    ///     Gets the page file at <paramref name="pageFileIndex" />, lazily opening it if not already open.
    /// </summary>
    /// <param name="pageFileIndex">The index of the page file in the tile set's page file list.</param>
    /// <returns>The page file, ready to read chunks from.</returns>
    public VirtualTexturePage Get(int pageFileIndex)
    {
        if (_open.TryGetValue(pageFileIndex, out var file)) return file;

        var stream = streamProvider(tileSet.PageFileInfos[pageFileIndex].FileName);
        if (!stream.CanSeek)
        {
            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            stream = buffer;
        }

        file = new VirtualTexturePage(tileSet, stream);

        _open[pageFileIndex] = file;
        return file;
    }
}