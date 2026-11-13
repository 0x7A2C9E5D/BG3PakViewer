namespace BG3PakViewer.VirtualTextures.PageFiles;

/// <summary>
///     Holds the open page file of a tile set, at most one at a time.
/// </summary>
/// <remarks>
///     When a page file other than the open one is requested, the open page file is closed before the new
///     one is opened, so the number of open GTP streams never grows with the size of the tile set. A page
///     stream that cannot seek is copied into memory first, because reading a page file seeks within it.
/// </remarks>
/// <param name="pageSize">The aligned size of one page in the page files, in bytes.</param>
/// <param name="pageStreamProvider">Opens the page file at the given index, e.g. from inside a PAK.</param>
internal sealed class VirtualTextureTilePageFileHolder(int pageSize, Func<int, Stream> pageStreamProvider) : IDisposable
{
    private int _openPageFileIndex = -1;
    private VirtualTextureTilePageFile? _pageFile;

    /// <summary>
    ///     Closes the page file that is still open.
    /// </summary>
    public void Dispose()
    {
        ClosePageFile();
    }

    /// <summary>
    ///     Gets the page file at <paramref name="pageFileIndex" />, closing the one that is open when it
    ///     is a different file.
    /// </summary>
    /// <param name="pageFileIndex">The index of the page file in the tile set's page file list.</param>
    /// <returns>The page file, ready to read chunks from.</returns>
    public VirtualTextureTilePageFile GetPageFile(int pageFileIndex)
    {
        if (_pageFile != null && _openPageFileIndex == pageFileIndex) return _pageFile;

        ClosePageFile();

        var stream = pageStreamProvider(pageFileIndex);
        if (!stream.CanSeek)
        {
            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            stream.Dispose();
            stream = buffer;
        }

        _pageFile = new VirtualTextureTilePageFile(pageSize, stream);
        _openPageFileIndex = pageFileIndex;
        return _pageFile;
    }

    /// <summary>
    ///     Closes the page file that is open, if any.
    /// </summary>
    private void ClosePageFile()
    {
        _pageFile?.Dispose();
        _pageFile = null;
        _openPageFileIndex = -1;
    }
}
