using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal class LarianResourceExportStrategy : IExportStrategy
{
    private static FileFilter[] Filters =>
    [
        new(Strings.LarianResourceFile, ".lsx"),
        new(Strings.LarianResourceFile, ".lsj"),
        new(Strings.LarianResourceFile, ".lsf"),
        new(Strings.LarianResourceFile, ".lsfx"),
        new(Strings.LarianResourceFile, ".lsb"),
        new(Strings.LarianResourceFile, ".lsbs")
    ];

    public async Task<bool> ExportAsync(Stream stream, string path, string extension)
    {
        if (extension.Equals(Path.GetExtension(path), StringComparison.OrdinalIgnoreCase))
            return await FileOperations.SaveStreamToFileAsync(path, stream);

        var resource = await LarianResourceLoader.LoadAsync(stream, extension);
        return resource != null && await LarianResourceLoader.ExportAsync(resource, path);
    }

    FileFilter[] IExportStrategy.Filters => Filters;
}