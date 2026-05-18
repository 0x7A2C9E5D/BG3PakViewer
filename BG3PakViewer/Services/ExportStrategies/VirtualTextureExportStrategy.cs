using System.IO;
using BG3PakViewer.Locales;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Serilog;

namespace BG3PakViewer.Services.ExportStrategies;

internal class VirtualTextureExportStrategy : IExportStrategy
{
    public FileFilter[] Filters =>
    [
        new(Strings.VirtualTextureFile, ".gts")
    ];

    public async Task<bool> ExportAsync(Stream stream, string path, string extension)
    {
        return await Task.Run(async () =>
        {
            try
            {
                var fs = File.Create(path);
                await stream.CopyToAsync(fs);
                await fs.FlushAsync();
                await fs.DisposeAsync();
                return true;
            }
            catch (Exception e)
            {
                Log.Error(e, "Error exporting virtual texture");
                return false;
            }
        });
    }
}