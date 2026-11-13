using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Cache of open GTP page files, lazily opened per pageFileIndex via the stream provider delegate
///     (e.g. read from inside a PAK), without relying on disk paths.
/// </summary>
internal sealed class VirtualTexturePageCache(VirtualTileSet tileSet, Func<int, Stream> streamProvider) : IDisposable
{
    private readonly Dictionary<int, VirtualTexturePage> _open = [];

    public void Dispose()
    {
        foreach (var file in _open.Values) file.Dispose();
        _open.Clear();
    }

    /// <summary>
    ///     Gets the page file at <paramref name="pageFileIndex" />, lazily opening it if not already open.
    /// </summary>
    /// <param name="pageFileIndex"></param>
    /// <returns></returns>
    public VirtualTexturePage Get(int pageFileIndex)
    {
        if (_open.TryGetValue(pageFileIndex, out var file)) return file;

        var stream = streamProvider(pageFileIndex);
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