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
        if (node.FileExtension.Equals(".wem", StringComparison.OrdinalIgnoreCase))
            return await FileOperations.SaveStreamToFileAsync(stream, path);
        return await WwiseAudioLoader.ExportAsync(stream, path);
    }
}