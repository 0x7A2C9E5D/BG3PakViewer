using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

/// <summary>
///     Cache of open GTP page files, lazily opened per pageFileIndex via the stream provider delegate
///     (e.g. read from inside a PAK), without relying on disk paths.
/// </summary>
internal sealed class PageFileCache(VirtualTileSet tileSet, Func<int, Stream> streamProvider) : IDisposable
{
    private readonly Dictionary<int, StreamingPageFile> _open = [];

    public void Dispose()
    {
        foreach (var file in _open.Values) file.Dispose();
        _open.Clear();
    }

    public StreamingPageFile Get(int pageFileIndex)
    {
        if (_open.TryGetValue(pageFileIndex, out var file)) return file;

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
}