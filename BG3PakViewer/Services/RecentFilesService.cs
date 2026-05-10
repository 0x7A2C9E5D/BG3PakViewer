using System.Collections.ObjectModel;
using BG3PakViewer.Contracts;
using BG3PakViewer.Models;
using Serilog;

namespace BG3PakViewer.Services;

internal class RecentFilesService(ObservableCollection<IRecentItem> recentItems) : IRecentFilesService
{
    public ObservableCollection<IRecentItem> RecentItems => recentItems;

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
            recentItems.Add(new RecentItem(filePath, DateTime.Now));
            Log.Information("Added new recent file: {FilePath}", filePath);
        }
    }

    public void RemoveRecentFile(IRecentItem recentItem)
    {
        if (recentItems.Remove(recentItem))
            Log.Information("Removed recent file: {FilePath}", recentItem.FilePath);
        else
            Log.Error("Failed to remove recent file: {FilePath}", recentItem.FilePath);
    }
}