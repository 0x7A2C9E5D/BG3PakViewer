using BG3PakViewer.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Models;

internal partial class RecentItem : ObservableObject, IRecentItem
{
    public RecentItem(string filePath, DateTime openedTime, bool isMarked = false)
    {
        IsMarked = isMarked;
        FilePath = filePath;
        OpenedTime = openedTime;
    }

    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    // ReSharper disable once MemberCanBePrivate.Global
    [ObservableProperty] public partial string FilePath { get; set; }

    [ObservableProperty] public partial bool IsMarked { get; set; }

    [ObservableProperty] public partial DateTime OpenedTime { get; set; }
}