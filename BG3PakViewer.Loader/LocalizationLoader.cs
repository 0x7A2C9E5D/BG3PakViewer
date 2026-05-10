using System.IO;
using System.Text;
using LSLib.LS;
using Serilog;

namespace BG3PakViewer.Loader;

public static class LocalizationLoader
{
    public static async Task<LocaResource?> LoadAsync(Stream stream)
    {
        try
        {
            return await Task.Run(() => LocaUtils.Load(stream, LocaFormat.Loca));
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load localization.");
            return null;
        }
    }

    public static async Task<string> ExportAsync(LocaResource resource)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var ms = new MemoryStream();
                var writer = new LocaXmlWriter(ms);
                writer.Write(resource);
                return Encoding.UTF8.GetString(ms.ToArray());
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export localization.");
            return string.Empty;
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