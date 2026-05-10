using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

public class AudioExportStrategy : IExportStrategy
{
    public FileFilter[] Filters =>
    [
        new(Strings.WwiseAudioFile, ".wem"),
        new(Strings.VorbisAudioFile, ".ogg")
    ];

    public async Task<bool> ExportAsync(Stream sourceStream, string targetPath, string sourceExtension)
    {
        if (Path.GetExtension(targetPath).Equals(".wem", StringComparison.OrdinalIgnoreCase))
            return await FileOperations.SaveStreamToFileAsync(targetPath, sourceStream);

        return await WwiseAudioLoader.ExportAsync(sourceStream, targetPath);
    }
}