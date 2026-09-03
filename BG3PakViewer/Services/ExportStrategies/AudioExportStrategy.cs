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

    public FileFilter[] GetExportFilters(string fileName, string fileExtension)
    {
        return
        [
            .. Filters.Where(f =>
                f.Extensions!.Any(ext => GetOperation(fileExtension, ext) != ExportOperation.Forbidden))
        ];
    }

    public async Task<bool> ExportAsync(PackageEntry node, string path)
    {
        await using var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
        if (stream is null) return false;
        return GetOperation(node.FileExtension, Path.GetExtension(path)) switch
        {
            ExportOperation.RawCopy => await FileOperations.SaveStreamToFileAsync(stream, path),
            ExportOperation.Convert => await WwiseAudioLoader.ExportAsync(stream, path),
            _ => false
        };
    }

    // Single source of truth for the supported export directions, shared by GetExportFilters
    // (dialog options) and ExportAsync (runtime guard):
    // WEM-to-OGG is the only supported conversion; an OGG source may only be copied as-is,
    // and can never be transcoded to WEM.
    private static ExportOperation GetOperation(string sourceExtension, string targetExtension)
    {
        if (FileExtensions.IsVorbisAudio(sourceExtension))
            return FileExtensions.IsVorbisAudio(targetExtension)
                ? ExportOperation.RawCopy
                : ExportOperation.Forbidden;
        if (FileExtensions.IsWwiseAudio(targetExtension))
            return ExportOperation.RawCopy;
        return FileExtensions.IsVorbisAudio(targetExtension)
            ? ExportOperation.Convert
            : ExportOperation.Forbidden;
    }
}