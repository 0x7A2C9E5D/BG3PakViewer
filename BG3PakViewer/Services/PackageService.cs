using System.Collections.ObjectModel;
using BG3PakViewer.Loader;
using BG3PakViewer.Shared.Models;
using LSLib.LS;
using Serilog;

namespace BG3PakViewer.Services;

/// <summary>
///     Package service
/// </summary>
/// <param name="packageLoader"></param>
internal sealed class PackageService(PackageLoader packageLoader) : IPackageService
{
    private bool _disposedValue;

    private Package? CurrentPackage { get; set; }

    /// <summary>
    ///     Is package loaded
    /// </summary>
    public bool IsLoaded => CurrentPackage != null;

    /// <summary>
    ///     Load package async
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    /// <exception cref="ObjectDisposedException"></exception>
    public async Task<bool> LoadPackageAsync(string path)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, nameof(PackageService));
        try
        {
            Log.Information("Loading package: {Path}", path);
            await CleanupAsync();
            CurrentPackage = await packageLoader.LoadAsync(path);
            if (CurrentPackage == null)
            {
                Log.Warning("Failed to load package: {Path}", path);
                return false;
            }

            var fileCount = CurrentPackage.Files.Count(f => !f.IsDeletion());
            Log.Information("Package loaded successfully. Active files: {Count}", fileCount);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading package: {Path}", path);
            CurrentPackage = null;
            return false;
        }
    }

    /// <summary>
    ///     Build tree
    /// </summary>
    /// <param name="searchQuery"></param>
    /// <returns></returns>
    public ObservableCollection<PackageEntry>? BuildTree(string? searchQuery = null)
    {
        if (CurrentPackage == null)
            return null;
        var files = string.IsNullOrWhiteSpace(searchQuery)
            ? GetValidFiles()
            : FilterFiles(searchQuery);
        var filePaths = files.Select(f => f.Name);
        return PackageEntry.BuildTree(filePaths);
    }

    /// <summary>
    ///     Get file by path
    /// </summary>
    /// <param name="fullPath"></param>
    /// <returns></returns>
    public PackagedFileInfo? GetFileByPath(string fullPath)
    {
        return CurrentPackage?.Files.FirstOrDefault(f =>
            f.Name.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Get valid files
    /// </summary>
    /// <returns></returns>
    public IEnumerable<PackagedFileInfo> GetValidFiles()
    {
        return CurrentPackage == null ? [] : CurrentPackage.Files.Where(f => !f.IsDeletion());
    }

    /// <summary>
    ///     Cleanup async
    /// </summary>
    public Task CleanupAsync()
    {
        if (CurrentPackage == null)
            return Task.CompletedTask;
        Log.Debug("Cleaning up current package");
        CurrentPackage.Dispose();
        CurrentPackage = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            CurrentPackage?.Dispose();
            CurrentPackage = null;
        }

        _disposedValue = true;
    }

    ~PackageService()
    {
        Dispose(false);
    }

    /// <summary>
    ///     Filter files
    /// </summary>
    /// <param name="searchQuery"></param>
    /// <returns></returns>
    private IEnumerable<PackagedFileInfo> FilterFiles(string searchQuery)
    {
        if (CurrentPackage == null || string.IsNullOrWhiteSpace(searchQuery))
            return [];
        return CurrentPackage.Files
            .Where(f => !f.IsDeletion() &&
                        f.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
    }
}