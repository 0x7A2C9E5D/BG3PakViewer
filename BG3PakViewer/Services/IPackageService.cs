using System.Collections.ObjectModel;
using BG3PakViewer.Shared.Models;
using LSLib.LS;

namespace BG3PakViewer.Services;

public interface IPackageService : IDisposable
{
    bool IsLoaded { get; }

    Task<bool> LoadPackageAsync(string path);

    ObservableCollection<PackageEntry>? BuildTree(string? searchQuery = null);

    PackagedFileInfo? GetFileByPath(string fullPath);

    IEnumerable<PackagedFileInfo> GetValidFiles();

    Task CleanupAsync();
}