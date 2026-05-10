using System.Collections.ObjectModel;

namespace BG3PakViewer.Contracts;

public interface IRecentFilesService
{
    ObservableCollection<IRecentItem> RecentItems { get; }
    
    void AddOrUpdateRecentFile(string filePath);

    void RemoveRecentFile(IRecentItem recentItem);
}