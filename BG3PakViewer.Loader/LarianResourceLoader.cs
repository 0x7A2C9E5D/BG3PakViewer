using System.IO;
using LSLib.LS;
using LSLib.LS.Enums;
using Serilog;

namespace BG3PakViewer.Loader;

/// <summary>
///     LarianResourceLoader
/// </summary>
public static class LarianResourceLoader
{
    /// <summary>
    ///     Loads a resource from a stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="extensions"></param>
    /// <returns></returns>
    public static Task<Resource> LoadAsync(Stream stream, string extensions)
    {
        var format = ResourceUtils.ExtensionToResourceFormat(extensions);
        return Task.Run(() =>
            ResourceUtils.LoadResource(stream, format, ResourceLoadParameters.FromGameVersion(Game.BaldursGate3)));
    }

    /// <summary>
    ///     Exports a resource to a file.
    /// </summary>
    /// <param name="resource"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static async Task<bool> ExportAsync(Resource resource, string path)
    {
        try
        {
            return await Task.Run(() =>
            {
                ResourceUtils.SaveResource(resource, path,
                    ResourceConversionParameters.FromGameVersion(Game.BaldursGate3));
                return true;
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export resource to {Path}", path);
            return false;
        }
    }
}