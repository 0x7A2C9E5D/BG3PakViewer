using System.IO;
using BG3PakViewer.Locales;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using LSLib.VirtualTextures;
using Serilog;

namespace BG3PakViewer.Services.ExportStrategies;

public class VirtualTextureExportStrategy : IExportStrategy
{
    public FileFilter[] Filters =>
    [
        new(Strings.VirtualTextureFile, ".gts")
    ];

    public async Task<bool> ExportAsync(Stream sourceStream, string targetPath, string sourceExtension)
    {
        return await Task.Run(async () =>
        {
            try
            {
                await using var targetStream = File.Create(targetPath);
                await sourceStream.CopyToAsync(targetStream);
                var tileSet = new VirtualTileSet(targetPath);
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