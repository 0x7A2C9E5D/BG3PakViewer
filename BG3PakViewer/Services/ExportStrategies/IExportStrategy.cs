using System.IO;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Services.ExportStrategies;

internal interface IExportStrategy
{
    FileFilter[] Filters { get; }

    Task<bool> ExportAsync(Stream stream, string path, string extension);
}