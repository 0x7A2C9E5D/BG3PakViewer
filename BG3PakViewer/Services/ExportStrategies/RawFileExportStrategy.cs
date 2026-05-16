using System.IO;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

public class RawFileExportStrategy : IExportStrategy
{
    public FileFilter[] Filters => [];

    public async Task<bool> ExportAsync(Stream sourceStream, string targetPath, string sourceExtension)
    {
        return await FileOperations.SaveStreamToFileAsync(targetPath, sourceStream);
    }
}