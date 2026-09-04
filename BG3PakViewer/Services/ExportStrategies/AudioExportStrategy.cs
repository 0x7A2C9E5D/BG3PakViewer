using System.IO;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

/// <summary>
///     Audio export strategy
/// </summary>
/// <param name="packageService"></param>
internal class AudioExportStrategy(IPackageService packageService) : IExportStrategy
{
    /// <summary>
    ///     File filters
    /// </summary>
    public FileFilter[] Filters =>
    [
        new(Strings.WwiseAudioFile, ".wem"),
        new(Strings.VorbisAudioFile, ".ogg")
    ];

    /// <summary>
    ///     Get export filters
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    public FileFilter[] GetExportFilters(string fileName, string fileExtension)
    {
        return
        [
            .. Filters.Where(f =>
                f.Extensions!.Any(ext => GetOperation(fileExtension, ext) != ExportOperation.Forbidden))
        ];
    }

    /// <summary>
    ///     Export async
    /// </summary>
    /// <param name="node"></param>
    /// <param name="path"></param>
    /// <returns></returns>
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

    /// <summary>
    ///     Get operation
    /// </summary>
    /// <param name="sourceExtension"></param>
    /// <param name="targetExtension"></param>
    /// <returns></returns>
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