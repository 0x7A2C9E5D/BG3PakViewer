using System.IO;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal class RawFileExportStrategy : IExportStrategy
{
    public FileFilter[] Filters => [];

    public async Task<bool> ExportAsync(Stream stream, string path, string extension)
    {
        return await FileOperations.SaveStreamToFileAsync(path, stream);
    }
}