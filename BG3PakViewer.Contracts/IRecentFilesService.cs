using System.Collections.ObjectModel;

namespace BG3PakViewer.Contracts;

/// <summary>
///     Recent files service
/// </summary>
public interface IRecentFilesService
{
    /// <summary>
    ///     Recent items
    /// </summary>
    ObservableCollection<IRecentFileEntry> RecentItems { get; }

    /// <summary>
    ///     Add or update recent file
    /// </summary>
    /// <param name="filePath"></param>
    void AddOrUpdateRecentFile(string filePath);

    /// <summary>
    ///     Remove recent file
    /// </summary>
    /// <param name="recentFileEntry"></param>
    void RemoveRecentFile(IRecentFileEntry recentFileEntry);
}