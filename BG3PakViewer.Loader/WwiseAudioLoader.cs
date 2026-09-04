using System.IO;
using Serilog;
using Ww2Ogg.Core;

namespace BG3PakViewer.Loader;

/// <summary>
///     WwiseAudioLoader
/// </summary>
public static class WwiseAudioLoader
{
    /// <summary>
    ///     Exports a Wwise audio file to a stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    private static async Task<Stream> ExportAsync(Stream stream)
    {
        var ms = new MemoryStream();
        var vorbis = new WwiseRiffVorbis(stream, CodebookLibrary.AoTuV);
        vorbis.GenerateOgg(ms);
        await ms.FlushAsync();
        ms.Position = 0;
        return ms;
    }

    /// <summary>
    ///     Exports a Wwise audio file to a file.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static async Task<bool> ExportAsync(Stream stream, string path)
    {
        try
        {
            await using var ms = await ExportAsync(stream);
            await using var fs = File.OpenWrite(path);
            await ms.CopyToAsync(fs);
            await fs.FlushAsync();
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to export audio to {Path}", path);
            return false;
        }
    }
}