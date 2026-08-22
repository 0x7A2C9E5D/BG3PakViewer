using System.IO;
using LSLib.LS;
using Serilog;

namespace BG3PakViewer.Loader;

public static class LocalizationLoader
{
    public static async Task<LocaResource?> LoadAsync(Stream stream)
    {
        try
        {
            // .loca files are native binary Loca resources; parse them directly.
            return await Task.Run(() => LocaUtils.Load(stream, LocaFormat.Loca));
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load localization.");
            return null;
        }
    }

    public static async Task<bool> ExportAsync(LocaResource resource, string path)
    {
        try
        {
            await Task.Run(() => { LocaUtils.Save(resource, path); });
            Log.Information("Saved localization to {Path}", path);
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to export localization.");
            return false;
        }
    }
}