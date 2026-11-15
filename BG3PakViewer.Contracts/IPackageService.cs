using LSLib.LS;

namespace BG3PakViewer.Contracts;

/// <summary>
///     Package service
/// </summary>
public interface IPackageService : IAsyncDisposable
{
    /// <summary>
    ///     Is package loaded
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    ///     A monotonically increasing value that changes whenever the loaded package is replaced or
    ///     released, including when a load attempt fails. A cache holding data read from the package
    ///     compares the version it was built from with this one to tell whether it is still valid.
    /// </summary>
    long PackageVersion { get; }

    /// <summary>
    ///     Loads a package, releasing the one that is currently open. The call advances
    ///     <see cref="PackageVersion" /> whether it succeeds or fails.
    /// </summary>
    /// <param name="path">The package file to load.</param>
    /// <returns><see langword="true" /> when the package was loaded.</returns>
    Task<bool> LoadPackageAsync(string path);

    /// <summary>
    ///     Get file by path
    /// </summary>
    /// <param name="fullPath"></param>
    /// <returns></returns>
    Task<PackagedFileInfo?> GetFileByPath(string fullPath);

    /// <summary>
    ///     Searches the package's active files, optionally by a case-insensitive path fragment.
    /// </summary>
    /// <param name="query">Path fragment to match; null or blank returns every active file.</param>
    /// <returns></returns>
    Task<IReadOnlyList<PackagedFileInfo>?> Search(string? query = null);
}
