using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

/// <summary>
/// 带 LRU 顺序的 GTP page 文件缓存，支持两种来源：
/// 1) 磁盘路径（page 文件已解包在磁盘上）
/// 2) 按 pageFileIndex 提供 <see cref="Stream"/> 的委托（如直接从 PAK 内读取）
/// </summary>
public sealed class PageFileCache : IDisposable
{
    private readonly VirtualTileSet _tileSet;
    private readonly Func<int, Stream>? _streamProvider;
    private readonly string[]? _paths;

    private readonly Dictionary<int, StreamPageFile> _open = [];
    private readonly Dictionary<int, LinkedListNode<int>> _nodes = [];
    private readonly LinkedList<int> _lru = [];

    /// <summary>从流来源构造：每次按 pageFileIndex 惰性打开对应 page 流。</summary>
    public PageFileCache(VirtualTileSet tileSet, Func<int, Stream> streamProvider)
    {
        _tileSet = tileSet;
        _streamProvider = streamProvider;
    }

    /// <summary>从磁盘路径构造：page 文件位于 <see cref="VirtualTileSet.PagePath"/> 目录下。</summary>
    public PageFileCache(VirtualTileSet tileSet)
    {
        _tileSet = tileSet;
        _paths = [.. tileSet.PageFileInfos.Select(f => Path.Join(tileSet.PagePath, f.FileName))];
    }

    public StreamPageFile Get(int pageFileIndex)
    {
        if (_nodes.TryGetValue(pageFileIndex, out var node))
        {
            _lru.Remove(node);
            _lru.AddFirst(node);
            return _open[pageFileIndex];
        }

        StreamPageFile file;
        if (_streamProvider != null)
        {
            var stream = _streamProvider(pageFileIndex);
            if (!stream.CanSeek)
            {
                var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                stream = buffer;
            }
            file = new StreamPageFile(_tileSet, stream);
        }
        else
        {
            file = new StreamPageFile(_tileSet, File.OpenRead(_paths![pageFileIndex]));
        }

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
