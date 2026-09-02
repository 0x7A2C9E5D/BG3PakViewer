using System.IO;
using Serilog;
using Ww2Ogg.Core;

namespace BG3PakViewer.Loader;

public static class WwiseAudioLoader
{
    private static async Task<Stream> ExportAsync(Stream stream)
    {
        var ms = new MemoryStream();
        var vorbis = new WwiseRiffVorbis(stream, CodebookLibrary.AoTuV);
        vorbis.GenerateOgg(ms);
        await ms.FlushAsync();
        ms.Position = 0;
        return ms;
    }

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