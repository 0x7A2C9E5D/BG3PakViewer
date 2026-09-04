namespace BG3PakViewer.Contracts;

/// <summary>
///     Recent file entry
/// </summary>
public interface IRecentFileEntry
{
    /// <summary>
    ///     File path
    /// </summary>
    string FilePath { get; }

    /// <summary>
    ///     Opened time
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    DateTime OpenedTime { get; set; }

    /// <summary>
    ///     Is marked
    /// </summary>
    bool IsMarked { get; set; }
}