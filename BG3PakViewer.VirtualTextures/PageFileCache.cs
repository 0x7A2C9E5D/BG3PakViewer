using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures;

/// <summary>
/// GTP page 文件缓存，按 pageFileIndex 通过委托惰性打开对应 page 流
/// （如直接从 PAK 内读取），不依赖磁盘路径。
/// GTS 通常只有 1~8 个 page 文件，无需 LRU 淘汰，纯 Dictionary 足够。
/// </summary>
public sealed class PageFileCache(VirtualTileSet tileSet, Func<int, Stream> streamProvider) : IDisposable
{
    private readonly Dictionary<int, StreamPageFile> _open = [];

    public StreamPageFile Get(int pageFileIndex)
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
        file = new StreamPageFile(tileSet, stream);

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
