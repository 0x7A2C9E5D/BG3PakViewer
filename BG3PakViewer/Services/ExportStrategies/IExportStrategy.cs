using System.IO;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

public interface IExportStrategy
{
    FileFilter[] Filters { get; }

    Task<bool> ExportAsync(Stream sourceStream, string targetPath, string sourceExtension);
}