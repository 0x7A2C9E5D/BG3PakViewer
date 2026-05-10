using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal class Model3DExportStrategy : IExportStrategy
{
    public FileFilter[] Filters =>
    [
        new(Strings.Granny3DFile, ".gr2"),
        new(Strings.GLTransmissionFormat, ".glb"),
        new(Strings.GLTransmissionFormat, ".gltf")
    ];

    public async Task<bool> ExportAsync(Stream sourceStream, string targetPath, string sourceExtension)
    {
        if (sourceExtension.Equals(Path.GetExtension(targetPath), StringComparison.OrdinalIgnoreCase))
            return await FileOperations.SaveStreamToFileAsync(targetPath, sourceStream);

        var root = await Model3DLoader.LoadAsync(sourceStream);
        return root != null && await Model3DLoader.ExportAsync(root, targetPath);
    }
}