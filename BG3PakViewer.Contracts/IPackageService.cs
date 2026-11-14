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
    ///     Loads a package, releasing the one that is currently open.
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
