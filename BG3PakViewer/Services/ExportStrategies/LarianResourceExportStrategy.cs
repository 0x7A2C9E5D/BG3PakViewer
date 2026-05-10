using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

public class LarianResourceExportStrategy : IExportStrategy
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

    public async Task<bool> ExportAsync(Stream sourceStream, string targetPath, string sourceExtension)
    {
        if (sourceExtension.Equals(Path.GetExtension(targetPath), StringComparison.OrdinalIgnoreCase))
            return await FileOperations.SaveStreamToFileAsync(targetPath, sourceStream);

        var resource = await ResourceLoader.LoadAsync(sourceStream, sourceExtension);
        return resource != null && await ResourceLoader.ExportAsync(resource, targetPath);
    }

    FileFilter[] IExportStrategy.Filters => Filters;
}