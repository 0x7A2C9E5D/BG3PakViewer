using System.Collections.ObjectModel;
using BG3PakViewer.Contracts;
using BG3PakViewer.Models;
using Serilog;

namespace BG3PakViewer.Services;

internal class RecentFilesService(ObservableCollection<IRecentFileEntry> recentItems) : IRecentFilesService
{
    public ObservableCollection<IRecentFileEntry> RecentItems => recentItems;

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

    public void RemoveRecentFile(IRecentFileEntry recentFileEntry)
    {
        if (recentItems.Remove(recentFileEntry))
            Log.Information("Removed recent file: {FilePath}", recentFileEntry.FilePath);
        else
            Log.Error("Failed to remove recent file: {FilePath}", recentFileEntry.FilePath);
    }
}