using LSLib.VirtualTextures;
using BG3PakViewer.VirtualTextures.Extraction;

namespace BG3PakViewer.VirtualTextures.Catalog;

/// <summary>
///     One open tile set of the package, ready to extract its textures.
/// </summary>
/// <remarks>
///     Owns the tile set stream and the loader, so releasing the source releases every page stream the
///     loader opened as well.
/// </remarks>
internal sealed class VirtualTextureSource : IDisposable
{
    private readonly VirtualTextureLoader _loader;
    private readonly IReadOnlyList<FourCCTextureMeta> _textures;
    private readonly Stream _tileSetStream;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VirtualTextureSource" /> class.
    /// </summary>
    /// <param name="gtsPath">The package path of the tile set.</param>
    /// <param name="tileSetStream">The stream of the tile set.</param>
    /// <param name="loader">The loader that extracts the textures.</param>
    /// <param name="textures">The textures of the tile set, in index order.</param>
    public VirtualTextureSource(string gtsPath, Stream tileSetStream, VirtualTextureLoader loader,
        IReadOnlyList<FourCCTextureMeta> textures)
    {
        GtsPath = gtsPath;
        _tileSetStream = tileSetStream;
        _loader = loader;
        _textures = textures;
    }

    /// <summary>
    ///     The package path of the tile set.
    /// </summary>
    public string GtsPath { get; }

    public void Dispose()
    {
        _loader.Dispose();
        _tileSetStream.Dispose();
    }

    /// <summary>
    ///     Extracts one layer of the texture at the given index.
    /// </summary>
    /// <param name="index">The index of the texture in the tile set's texture list.</param>
    /// <param name="layer">The layer index.</param>
    /// <param name="progress">Reports the extraction progress, in percent.</param>
    /// <param name="cancellationToken">Cancels the extraction.</param>
    /// <returns>The layer data, or <see langword="null" /> when the layer contains no data.</returns>
    public Task<Stream?> ExtractAsync(int index, int layer, IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return _loader.ExtractAsync(_textures[index], layer, progress, cancellationToken);
    }
}