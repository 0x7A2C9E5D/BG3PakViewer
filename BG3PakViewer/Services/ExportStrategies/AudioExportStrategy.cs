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

    public async Task<bool> ExportAsync(Stream stream, string path, string extension)
    {
        if (Path.GetExtension(path).Equals(".wem", StringComparison.OrdinalIgnoreCase))
            return await FileOperations.SaveStreamToFileAsync(path, stream);

        return await WwiseAudioLoader.ExportAsync(stream, path);
    }
}