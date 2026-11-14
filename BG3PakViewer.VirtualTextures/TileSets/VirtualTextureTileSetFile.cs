using System.Text;
using LSLib.VirtualTextures;

namespace BG3PakViewer.VirtualTextures.TileSets;

/// <summary>
///     A tile set (.gts) parsed from a stream: owns the stream and the parsed tile set, and exposes the
///     texture metadata the rest of the pipeline reads.
/// </summary>
/// <remarks>
///     Parsing opens no GTP page file; the page files are separate streams the layer extraction asks a
///     provider for. The tile ranges are not computed here either, because only an extraction needs them.
/// </remarks>
internal sealed class VirtualTextureTileSetFile : IAsyncDisposable
{
    private readonly Stream _stream;
    private bool _disposed;

    /// <summary>
    ///     Reads the tile set from <paramref name="stream" />, which this object owns from then on.
    /// </summary>
    public VirtualTextureTileSetFile(Stream stream)
    {
        try
        {
            _stream = stream;
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            TileSet = new VirtualTileSet();
            TileSet.LoadFromStream(stream, reader, false);
            Textures = TileSet.FourCCMetadata.ExtractTextureMetadata();
        }
        finally
        {
            _stream?.Dispose();
        }
    }

    /// <summary>
    ///     The textures of the tile set, in tile set order, which is the order the layer metadata is
    ///     indexed in.
    /// </summary>
    internal IReadOnlyList<FourCCTextureMeta> Textures { get; }

    /// <summary>
    ///     The parsed tile set, which the extraction reads its tiles through.
    /// </summary>
    internal VirtualTileSet TileSet { get; }

    /// <summary>
    ///     The number of layers in the virtual texture.
    /// </summary>
    internal int LayerCount => TileSet.TileSetLayers.Length;

    /// <summary>
    ///     Releases the parsed tile set and the stream it was read from.
    /// </summary>
    /// <remarks>
    ///     Both are synchronous to close; <see cref="IAsyncDisposable" /> lets callers release them with
    ///     <c>await using</c> at the end of an asynchronous scope instead of a blocking disposal.
    /// </remarks>
    /// <returns>A task that completes when the tile set is released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        TileSet.Dispose();
        await _stream.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }
}
