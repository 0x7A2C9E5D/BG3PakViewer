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
internal sealed class VirtualTextureTileSetFile : IDisposable
{
    private readonly Stream _stream;

    /// <summary>
    ///     Reads the tile set from <paramref name="stream" />, which this object owns from then on.
    /// </summary>
    /// <param name="streamTask">Opens the tile set stream, which this object owns from then on.</param>
    public VirtualTextureTileSetFile(Task<Stream> streamTask)
    {
        // The package service opens the stream asynchronously; parsing is synchronous and runs on a
        // worker, so waiting here cannot deadlock on a UI synchronization context.
        var stream = streamTask.GetAwaiter().GetResult();
        _stream = stream;
        try
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            TileSet = new VirtualTileSet();
            TileSet.LoadFromStream(stream, reader, false);
            Textures = TileSet.FourCCMetadata.ExtractTextureMetadata();
        }
        catch
        {
            // This object owns the stream now, so a failed parse must not leave it open.
            stream.Dispose();
            throw;
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
    public void Dispose()
    {
        TileSet.Dispose();
        _stream.Dispose();
    }
}