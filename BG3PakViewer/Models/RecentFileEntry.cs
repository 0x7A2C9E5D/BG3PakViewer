using BG3PakViewer.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Models;

/// <summary>
///     Recent file entry
/// </summary>
internal partial class RecentFileEntry : ObservableObject, IRecentFileEntry
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RecentFileEntry" /> class.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="openedTime"></param>
    /// <param name="isMarked"></param>
    public RecentFileEntry(string filePath, DateTime openedTime, bool isMarked = false)
    {
        IsMarked = isMarked;
        FilePath = filePath;
        OpenedTime = openedTime;
    }

    /// <summary>
    ///     File path
    /// </summary>
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    // ReSharper disable once MemberCanBePrivate.Global
    [ObservableProperty]
    public partial string FilePath { get; set; }

    /// <summary>
    ///     Is marked
    /// </summary>
    [ObservableProperty]
    public partial bool IsMarked { get; set; }

    /// <summary>
    ///     Opened time
    /// </summary>
    [ObservableProperty]
    public partial DateTime OpenedTime { get; set; }
}