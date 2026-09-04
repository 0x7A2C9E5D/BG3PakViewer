using System.IO;
using System.Text;
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
    public static async Task<Resource?> LoadAsync(Stream stream, string extensions)
    {
        var format = ResourceUtils.ExtensionToResourceFormat(extensions);
        return await Task.Run(() => LoadAsync(stream, format));
    }

    /// <summary>
    ///     Loads a resource from a stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="format"></param>
    /// <returns></returns>
    private static async Task<Resource?> LoadAsync(Stream stream, ResourceFormat format)
    {
        return await Task.Run(() =>
            ResourceUtils.LoadResource(stream, format, ResourceLoadParameters.FromGameVersion(Game.BaldursGate3)));
    }
    
    /// <summary>
    ///     Exports a resource to a string.
    /// </summary>
    /// <param name="resource"></param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public static async Task<string> ExportAsync(Resource resource)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var ms = new MemoryStream();
                var writer = new LSXWriter(ms)
                {
                    Version = LSXVersion.V4,
                    PrettyPrint = true
                };
                writer.Write(resource);
                return Encoding.UTF8.GetString(ms.ToArray());
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export resource.");
            return string.Empty;
        }
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