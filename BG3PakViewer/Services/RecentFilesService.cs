using System.Collections.ObjectModel;
using BG3PakViewer.Contracts;
using BG3PakViewer.Models;
using Serilog;

namespace BG3PakViewer.Services;

/// <summary>
///     Recent files service
/// </summary>
/// <param name="recentItems"></param>
internal class RecentFilesService(ObservableCollection<IRecentFileEntry> recentItems) : IRecentFilesService
{
    /// <summary>
    ///     Recent items
    /// </summary>
    public ObservableCollection<IRecentFileEntry> RecentItems => recentItems;

    /// <summary>
    ///     Add or update recent file
    /// </summary>
    /// <param name="filePath"></param>
    public void AddOrUpdateRecentFile(string filePath)
    {
        var existingItem = recentItems.FirstOrDefault(x =>
            string.Equals(x.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (existingItem != null)
        {
            existingItem.OpenedTime = DateTime.Now;
            Log.Debug("Updated recent file: {FilePath}", filePath);
        }
        else
        {
            recentItems.Add(new RecentFileEntry(filePath, DateTime.Now));
            Log.Information("Added new recent file: {FilePath}", filePath);
        }
    }

    /// <summary>
    ///     Remove recent file
    /// </summary>
    /// <param name="recentFileEntry"></param>
    public void RemoveRecentFile(IRecentFileEntry recentFileEntry)
    {
        if (recentItems.Remove(recentFileEntry))
            Log.Information("Removed recent file: {FilePath}", recentFileEntry.FilePath);
        else
            Log.Warning("Recent file to remove was not in the list: {FilePath}", recentFileEntry.FilePath);
    }
}