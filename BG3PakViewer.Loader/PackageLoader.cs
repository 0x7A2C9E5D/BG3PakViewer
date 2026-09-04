using LSLib.LS;
using Serilog;

namespace BG3PakViewer.Loader;

/// <summary>
///     PackageLoader
/// </summary>
public class PackageLoader
{
    private readonly PackageReader _packageReader = new();

    /// <summary>
    ///     Loads a package from a file.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
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