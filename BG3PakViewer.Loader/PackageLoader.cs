using LSLib.LS;
using Serilog;

namespace BG3PakViewer.Loader;

public class PackageLoader
{
    private readonly PackageReader _packageReader = new();

    public async Task<Package?> LoadAsync(string path)
    {
        try
        {
            return await Task.Run(() => _packageReader.Read(path));
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load package: {Path}", path);
            return null;
        }
    }
}