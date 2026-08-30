using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

public sealed class PageFileCache : IDisposable
{
    private readonly VirtualTileSet _tileSet;
    private readonly string[] _paths;
    private readonly Dictionary<int, PageFile> _open = [];
    private readonly Dictionary<int, LinkedListNode<int>> _nodes = [];
    private readonly LinkedList<int> _lru = [];


    public PageFileCache(VirtualTileSet tileSet)
    {
        _tileSet = tileSet;
        _paths = [.. tileSet.PageFileInfos.Select(f => Path.Join(tileSet.PagePath, f.FileName))];
    }

    public PageFile Get(int pageFileIndex)
    {
        if (_nodes.TryGetValue(pageFileIndex, out var node))
        {
            _lru.Remove(node);
            _lru.AddFirst(node);
            return _open[pageFileIndex];
        }

        var file = new PageFile(_tileSet, _paths[pageFileIndex]);
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