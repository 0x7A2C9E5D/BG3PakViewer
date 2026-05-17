using System.Collections.ObjectModel;

namespace BG3PakViewer.Contracts;

public interface IAppSettings
{
    public ObservableCollection<IRecentItem> RecentItems { get; }

    public string Language { get; set; }

    public string NexusModUrl { get; }

    public int MaxPreviewLines { get; set; }

    public string DefaultOpenDirectory { get; set; }

    public string DefaultExportDirectory { get; set; }
}