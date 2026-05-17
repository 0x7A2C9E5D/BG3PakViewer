using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

public class LocalizationExportStrategy : IExportStrategy
{
    public FileFilter[] Filters =>
    [
        new(Strings.XmlFile, ".xml"),
        new(Strings.LarianResourceFile, ".loca")
    ];

    public async Task<bool> ExportAsync(Stream stream, string path, string extension)
    {
        if (extension.Equals(Path.GetExtension(path), StringComparison.OrdinalIgnoreCase))
            return await FileOperations.SaveStreamToFileAsync(path, stream);

        var resource = await LocalizationLoader.LoadAsync(stream);
        return resource != null && await LocalizationLoader.ExportAsync(resource, path);
    }
}