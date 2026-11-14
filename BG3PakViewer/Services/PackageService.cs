using BG3PakViewer.Contracts;
using BG3PakViewer.Loader;
using LSLib.LS;
using Serilog;

namespace BG3PakViewer.Services;

/// <summary>
///     Package service
/// </summary>
internal sealed class PackageService : IPackageService
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
            CurrentPackage = await PackageLoader.LoadAsync(path);
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
    ///     Searches the package's active files, optionally by a case-insensitive path fragment.
    /// </summary>
    /// <param name="query">Path fragment to match; null or blank returns every active file.</param>
    /// <returns></returns>
    public IEnumerable<PackagedFileInfo> Search(string? query = null)
    {
        if (CurrentPackage == null) return [];

        var files = CurrentPackage.Files.Where(f => !f.IsDeletion());
        return string.IsNullOrWhiteSpace(query)
            ? files
            : files.Where(f => f.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
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

    /// <summary>
    ///     Releases the open package.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Releases the open package; managed resources only when called from <see cref="Dispose()" />.
    /// </summary>
    /// <param name="disposing">Whether managed resources are released.</param>
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

    /// <summary>
    ///     Finalizer that releases the package when <see cref="Dispose()" /> was not called.
    /// </summary>
    ~PackageService()
    {
        Dispose(false);
    }
}