using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using BG3PakViewer.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Models;

/// <summary>
///     Application settings
/// </summary>
internal partial class AppSettings : ObservableObject, IAppSettings
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AppSettings" /> class.
    /// </summary>
    public AppSettings()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AppSettings" /> class.
    /// </summary>
    /// <param name="recentItems"></param>
    /// <param name="language"></param>
    [JsonConstructor]
    public AppSettings(ObservableCollection<IRecentFileEntry> recentItems, string language)
    {
        Language = language;
        RecentItems = [.. recentItems];
    }

    /// <summary>
    ///     Language
    /// </summary>
    [ObservableProperty]
    public partial string Language { get; set; } = string.Empty;

    /// <summary>
    ///     Recent items
    /// </summary>
    public ObservableCollection<IRecentFileEntry> RecentItems { get; } = [];

    /// <summary>
    ///     Nexus mod url
    /// </summary>
    [JsonIgnore]
    public string NexusModUrl => "https://www.nexusmods.com/baldursgate3/mods/22713";

    /// <summary>
    ///     Max preview lines
    /// </summary>
    [ObservableProperty]
    public partial int MaxPreviewLines { get; set; } = 500;

    /// <summary>
    ///     Default open directory
    /// </summary>
    [ObservableProperty]
    public partial string DefaultOpenDirectory { get; set; } = string.Empty;

    /// <summary>
    ///     Default export directory
    /// </summary>
    [ObservableProperty]
    public partial string DefaultExportDirectory { get; set; } = string.Empty;
}