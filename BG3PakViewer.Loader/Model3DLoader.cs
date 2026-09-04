using System.IO;
using LSLib.Granny.GR2;
using LSLib.Granny.Model;
using LSLib.LS.Enums;
using Serilog;

namespace BG3PakViewer.Loader;

/// <summary>
///     Model3DLoader
/// </summary>
public static class Model3DLoader
{
    /// <summary>
    ///     Loads a model from a stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public static async Task<Root?> LoadAsync(Stream stream)
    {
        try
        {
            return await Task.Run(() =>
            {
                var root = Root.CreateEmpty();
                var reader = new GR2Reader(stream);
                reader.Read(root);
                root.PostLoad(reader.Tag);
                return root;
            });
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load model.");
            return null;
        }
    }

    /// <summary>
    ///     Exports a model to a file.
    /// </summary>
    /// <param name="root"></param>
    /// <param name="path"></param>
    /// <param name="format"></param>
    /// <returns></returns>
    private static async Task<bool> ExportAsync(Root root, string path, ExportFormat format)
    {
        try
        {
            await Task.Run(() =>
            {
                var options = new ExporterOptions
                {
                    Input = root
                };
                options.LoadGameSettings(Game.BaldursGate3);
                options.OutputPath = path;
                options.OutputFormat = format;
                options.BuildDummySkeleton = root.Skeletons == null;
                var exporter = new Exporter
                {
                    Options = options
                };
                exporter.Export();
            });
            Log.Information("Exported model to {Path}", path);
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to export model to {Path}", path);
            return false;
        }
    }

    /// <summary>
    ///     Exports a model to a file.
    /// </summary>
    /// <param name="root"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static async Task<bool> ExportAsync(Root root, string path)
    {
        var format = GetExportFormatFromExtension(path);
        return await ExportAsync(root, path, format);
    }

    /// <summary>
    ///     Gets the export format from the file extension.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static ExportFormat GetExportFormatFromExtension(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".dae" => ExportFormat.DAE,
            ".glb" => ExportFormat.GLB,
            ".gltf" => ExportFormat.GLTF,
            _ => ExportFormat.GR2
        };
    }
}