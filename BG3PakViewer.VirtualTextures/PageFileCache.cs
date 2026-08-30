using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

/// <summary>
/// 带 LRU 顺序的 GTP page 文件缓存，按 pageFileIndex 通过委托惰性打开对应 page 流
/// （如直接从 PAK 内读取），不依赖磁盘路径。
/// </summary>
public sealed class PageFileCache(VirtualTileSet tileSet, Func<int, Stream> streamProvider) : IDisposable
{
    private readonly Dictionary<int, StreamPageFile> _open = [];
    private readonly Dictionary<int, LinkedListNode<int>> _nodes = [];
    private readonly LinkedList<int> _lru = [];

    public StreamPageFile Get(int pageFileIndex)
    {
        if (_nodes.TryGetValue(pageFileIndex, out var node))
        {
            _lru.Remove(node);
            _lru.AddFirst(node);
            return _open[pageFileIndex];
        }

        var stream = streamProvider(pageFileIndex);
        if (!stream.CanSeek)
        {
            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            stream = buffer;
        }
        var file = new StreamPageFile(tileSet, stream);

        _open[pageFileIndex] = file;
        _nodes[pageFileIndex] = _lru.AddFirst(pageFileIndex);
        return file;
    }

    public void Dispose()
    {
        foreach (var file in _open.Values)
        {
            file.Dispose();
        }
        _open.Clear();
        _nodes.Clear();
        _lru.Clear();
    }
}
