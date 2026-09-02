using System.IO;
using BG3PakViewer.Locales;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal class VirtualTextureExportStrategy : IExportStrategy
{
    public FileFilter[] Filters =>
    [
        new(Strings.VirtualTextureFile, ".gts")
    ];

    public async Task<bool> ExportAsync(Stream stream, string path, string extension)
    {
        return await FileOperations.SaveStreamToFileAsync(path, stream);
    }
}