using BG3PakViewer.Contracts;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Resolves the GTP page files of one tile set into package streams.
/// </summary>
/// <remarks>
///     The tile set names its page files relative to itself, so this source maps a page file index to the
///     package path next to the tile set and opens it. It owns no stream: every call returns a new one
///     that the caller disposes.
/// </remarks>
/// <param name="packageService">The loaded package the page files are read from.</param>
/// <param name="tileSet">The tile set that names its page files.</param>
/// <param name="tileSetPath">The package path of the tile set the page files belong to.</param>
internal sealed class VirtualTexturePageFileSource(
    IPackageService packageService,
    VirtualTileSet tileSet,
    string tileSetPath)
{
    private readonly string? _directory = Path.GetDirectoryName(tileSetPath)?.Replace('\\', '/');

    /// <summary>
    ///     Opens the page file at the given index, next to the tile set that names it.
    /// </summary>
    /// <param name="pageFileIndex">The index of the page file in the tile set's page file list.</param>
    /// <returns>The content of the page file.</returns>
    /// <exception cref="FileNotFoundException">The package does not contain the page file.</exception>
    public async Task<Stream> OpenAsync(int pageFileIndex)
    {
        var fileName = tileSet.PageFileInfos[pageFileIndex].FileName;
        var path = string.IsNullOrEmpty(_directory) ? fileName : $"{_directory}/{fileName}";
        return (await packageService.GetFileByPath(path).ConfigureAwait(false))?.CreateContentReader()
               ?? throw new FileNotFoundException($"Package file not found: {path}");
    }
}
