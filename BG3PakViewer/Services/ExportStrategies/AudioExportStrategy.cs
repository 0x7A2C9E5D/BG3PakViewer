using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal class AudioExportStrategy(IPackageService packageService) : IExportStrategy
{
    public FileFilter[] Filters =>
    [
        new(Strings.WwiseAudioFile, ".wem"),
        new(Strings.VorbisAudioFile, ".ogg")
    ];

    public async Task<bool> ExportAsync(PackageEntry node, string path)
    {
        await using var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
        if (stream is null) return false;
        var sourceExtension = node.FileExtension;
        var targetExtension = Path.GetExtension(path);

        // WEM-to-OGG is a one-way conversion: OGG sources can only be copied as-is, never transcoded to WEM.
        if (sourceExtension.Equals(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            if (!targetExtension.Equals(".ogg", StringComparison.OrdinalIgnoreCase))
                return false;
            return await FileOperations.SaveStreamToFileAsync(stream, path);
        }

        // WEM sources: copy as-is for .wem targets, transcode for .ogg targets; other targets are unsupported.
        if (targetExtension.Equals(".wem", StringComparison.OrdinalIgnoreCase))
            return await FileOperations.SaveStreamToFileAsync(stream, path);
        return targetExtension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            && await WwiseAudioLoader.ExportAsync(stream, path);
    }
}