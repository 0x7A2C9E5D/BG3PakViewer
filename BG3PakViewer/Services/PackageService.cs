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
    private Package? _currentPackage;
    private long _packageVersion;

    private Package? CurrentPackage => Volatile.Read(ref _currentPackage);

    /// <summary>
    ///     Is package loaded
    /// </summary>
    public bool IsLoaded => CurrentPackage != null;

    /// <summary>
    ///     Changes whenever the loaded package is replaced or released, including a failed load.
    ///     Caches that hold data read from the package compare this with the version they were built
    ///     from to tell whether they are stale.
    /// </summary>
    public long PackageVersion => Interlocked.Read(ref _packageVersion);

    /// <summary>
    ///     Replaces the current package and invalidates the caches built from the previous one. The
    ///     version is bumped after the package becomes visible, so a reader that sees the new version
    ///     can never still read the old package.
    /// </summary>
    /// <param name="package">The package now loaded, or <see langword="null" /> when none is.</param>
    private void SetCurrentPackage(Package? package)
    {
        Volatile.Write(ref _currentPackage, package);
        Interlocked.Increment(ref _packageVersion);
    }

    /// <summary>
    ///     Loads a package, releasing the one that is currently open. Every outcome advances
    ///     <see cref="PackageVersion" />, so caches built from the previous package go stale even
    ///     when the load fails.
    /// </summary>
    /// <param name="path">The package file to load.</param>
    /// <returns><see langword="true" /> when the package was loaded.</returns>
    /// <exception cref="ObjectDisposedException">The service has already been disposed.</exception>
    public async Task<bool> LoadPackageAsync(string path)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, nameof(PackageService));
        try
        {
            Log.Information("Loading package: {Path}", path);
            if (CurrentPackage != null) await CleanupAsync().ConfigureAwait(false);
            SetCurrentPackage(await PackageLoader.LoadAsync(path).ConfigureAwait(false));
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
            SetCurrentPackage(null);
            return false;
        }
    }

    /// <summary>
    ///     Get file by path
    /// </summary>
    /// <param name="fullPath"></param>
    /// <returns></returns>
    public async Task<PackagedFileInfo?> GetFileByPath(string fullPath)
    {
        var package = CurrentPackage;
        if (package is null) return null;

        var file = package.Files.FirstOrDefault(f =>
            f.Name.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        return await Task.FromResult(file).ConfigureAwait(false);
    }

    /// <summary>
    ///     Searches the package's active files, optionally by a case-insensitive path fragment.
    /// </summary>
    /// <param name="query">Path fragment to match; null or blank returns every active file.</param>
    /// <returns>The matching files, or <see langword="null" /> when no package is loaded.</returns>
    public Task<IReadOnlyList<PackagedFileInfo>?> Search(string? query = null)
    {
        var package = CurrentPackage;
        if (package is null) return Task.FromResult<IReadOnlyList<PackagedFileInfo>?>(null);

        var files = package.Files.Where(f => !f.IsDeletion());
        if (!string.IsNullOrWhiteSpace(query))
            files = files.Where(f => f.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<PackagedFileInfo>?>([.. files]);
    }

    /// <summary>
    ///     Releases the open package and marks the service as disposed. Replacing a package calls
    ///     <see cref="CleanupAsync" /> on its own and never disposes the service.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposedValue) return;
        await CleanupAsync().ConfigureAwait(false);
        _disposedValue = true;
    }

    /// <summary>
    ///     Releases the current package so another one can be loaded, advancing
    ///     <see cref="PackageVersion" /> before the old package is disposed on a worker.
    /// </summary>
    private Task CleanupAsync()
    {
        var package = CurrentPackage;
        if (package is null) return Task.CompletedTask;
        SetCurrentPackage(null);
        Log.Debug("Cleaning up current package");
        return Task.Run(package.Dispose);
    }
}
