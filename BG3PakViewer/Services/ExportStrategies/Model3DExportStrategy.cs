using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal class Model3DExportStrategy(IPackageService packageService) : IExportStrategy
{
    public FileFilter[] Filters =>
    [
        new(Strings.Granny3DFile, ".gr2"),
        new(Strings.GLTransmissionFormat, ".glb"),
        new(Strings.GLTransmissionFormat, ".gltf")
    ];

    public async Task<bool> ExportAsync(PackageEntry node, string path)
    {
        await using var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
        if (stream is null) return false;
        if (node.FileExtension.Equals(Path.GetExtension(path), StringComparison.OrdinalIgnoreCase))
            return await FileOperations.SaveStreamToFileAsync(stream, path);
        var root = await Model3DLoader.LoadAsync(stream);
        return root != null && await Model3DLoader.ExportAsync(root, path);
    }
}