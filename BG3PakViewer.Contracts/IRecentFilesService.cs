using System.Collections.ObjectModel;

namespace BG3PakViewer.Contracts;

public interface IRecentFilesService
{
    ObservableCollection<IRecentFileEntry> RecentItems { get; }

    void AddOrUpdateRecentFile(string filePath);

    void RemoveRecentFile(IRecentFileEntry recentFileEntry);
}