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

    public async Task<bool> ExportAsync(Stream sourceStream, string targetPath, string sourceExtension)
    {
        if (sourceExtension.Equals(Path.GetExtension(targetPath), StringComparison.OrdinalIgnoreCase))
            return await FileOperations.SaveStreamToFileAsync(targetPath, sourceStream);

        var resource = await LocalizationLoader.LoadAsync(sourceStream);
        return resource != null && await LocalizationLoader.ExportAsync(resource, targetPath);
    }
}