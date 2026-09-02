using System.IO;
using BG3PakViewer.VirtualTextures;
using LSLib.VirtualTextures;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Loader;

/// <summary>
///     High-level facade over <see cref="VirtualTileSetExtractor" />: exposes only the pieces a
///     preview/export caller needs (layer count, texture metadata, DDS extraction and decoding),
///     hiding LSLib tile set, paging and unpacking internals.
/// </summary>
public sealed class VirtualTextureLoader(VirtualTileSetExtractor extractor) : IDisposable
{
    public int LayerCount => extractor.LayerCount;

    public IReadOnlyList<FourCCTextureMeta> GetTextures() => extractor.GetTextures();

    /// <summary>
    ///     Extracts <paramref name="layer" /> of <paramref name="meta" /> into a seekable DDS stream,
    ///     reporting progress in percent; returns null when the layer contains no data.
    /// </summary>
    public async Task<Stream?> ExtractAsync(FourCCTextureMeta meta, int layer,
        IProgress<double>? progress, CancellationToken ct)
    {
        var ddsStream = new MemoryStream();
        var transferred = false;
        try
        {
            var extracted = await Task.Run(
                () => extractor.ExtractTexture(layer, meta, ddsStream, CreateTileProgress(progress), ct), ct);
            if (!extracted) return null;

            ddsStream.Position = 0;
            transferred = true;
            return ddsStream;
        }
        finally
        {
            if (!transferred) await ddsStream.DisposeAsync();
        }
    }

    /// <summary>Decodes a DDS stream produced by <see cref="ExtractAsync" /> into an image.</summary>
    public static async Task<Image?> DecodeAsync(Stream ddsStream)
    {
        return await ImageLoader.LoadAsync(ddsStream, ".dds");
    }

    public void Dispose() => extractor.Dispose();

    private static Progress<(int Done, int Total)>? CreateTileProgress(IProgress<double>? progress)
    {
        if (progress is null) return null;
        return new Progress<(int Done, int Total)>(p =>
            progress.Report(p.Total == 0 ? 0 : p.Done * 100.0 / p.Total));
    }
}
