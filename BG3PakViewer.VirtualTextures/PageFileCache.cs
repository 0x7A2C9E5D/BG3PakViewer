using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

/// <summary>
/// Cache of open GTP page files, lazily opened per pageFileIndex via the stream provider delegate
/// (e.g. read from inside a PAK), without relying on disk paths.
/// A GTS usually references only 1~8 page files, so no LRU eviction is needed; a plain dictionary suffices.
/// </summary>
public sealed class PageFileCache(VirtualTileSet tileSet, Func<int, Stream> streamProvider) : IDisposable
{
    private readonly Dictionary<int, StreamingPageFile> _open = [];

    public StreamingPageFile Get(int pageFileIndex)
    {
        if (_open.TryGetValue(pageFileIndex, out var file))
        {
            return file;
        }

        var stream = streamProvider(pageFileIndex);
        if (!stream.CanSeek)
        {
            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            stream = buffer;
        }
        file = new StreamingPageFile(tileSet, stream);

        _open[pageFileIndex] = file;
        return file;
    }

    public void Dispose()
    {
        foreach (var file in _open.Values)
        {
            file.Dispose();
        }
        _open.Clear();
    }
}
