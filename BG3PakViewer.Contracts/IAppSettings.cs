using System.Collections.ObjectModel;

namespace BG3PakViewer.Contracts;

/// <summary>
///     App settings
/// </summary>
public interface IAppSettings
{
    /// <summary>
    ///     Recent items
    /// </summary>
    public ObservableCollection<IRecentFileEntry> RecentItems { get; }

    /// <summary>
    ///     Language
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    ///     Nexusmods url
    /// </summary>
    public string NexusModUrl { get; }

    /// <summary>
    ///     Max preview lines
    /// </summary>
    public int MaxPreviewLines { get; set; }

    /// <summary>
    ///     Default open directory
    /// </summary>
    public string DefaultOpenDirectory { get; set; }

    /// <summary>
    ///     Default export directory
    /// </summary>
    public string DefaultExportDirectory { get; set; }
}