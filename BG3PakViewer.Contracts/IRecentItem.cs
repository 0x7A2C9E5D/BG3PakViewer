namespace BG3PakViewer.Contracts;

public interface IRecentItem
{
    public string FilePath { get; }

    // ReSharper disable once UnusedMember.Global
    public DateTime OpenedTime { get; set; }

    public bool IsMarked { get; set; }
}