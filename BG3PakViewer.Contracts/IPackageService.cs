using LSLib.LS;

namespace BG3PakViewer.Contracts;

/// <summary>
///     Package service
/// </summary>
public interface IPackageService : IDisposable
{
    /// <summary>
    ///     Is package loaded
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    ///     Load package async
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    Task<bool> LoadPackageAsync(string path);

    /// <summary>
    ///     Get file by path
    /// </summary>
    /// <param name="fullPath"></param>
    /// <returns></returns>
    PackagedFileInfo? GetFileByPath(string fullPath);

    /// <summary>
    ///     Get valid files
    /// </summary>
    /// <returns></returns>
    IEnumerable<PackagedFileInfo> GetValidFiles();

    /// <summary>
    ///     Cleanup async
    /// </summary>
    /// <returns></returns>
    Task CleanupAsync();
}