using LSLib.LS;
using Serilog;

namespace BG3PakViewer.Loader;

/// <summary>
///     Loads packages from files.
/// </summary>
public static class PackageLoader
{
    /// <summary>
    ///     Loads a package from a file.
    /// </summary>
    /// <param name="path"></param>
    /// <returns>The loaded package, or null when the file could not be read.</returns>
    public static async Task<Package?> LoadAsync(string path)
    {
        try
        {
            // A reader parks the package it is building in an instance field while it parses, and
            // disposes that field when the read fails. A shared reader would therefore let one
            // failed load dispose the package another load is still building, so every load gets
            // its own reader.
            return await Task.Run(() => new PackageReader().Read(path));
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load package: {Path}", path);
            return null;
        }
    }
}
