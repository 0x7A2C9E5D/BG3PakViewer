using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using BG3PakViewer.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Models;

internal partial class AppSettings : ObservableObject, IAppSettings
{
    public AppSettings()
    {
    }

    [JsonConstructor]
    public AppSettings(ObservableCollection<IRecentItem> recentItems, string language)
    {
        Language = language;
        RecentItems = [.. recentItems];
    }

    [ObservableProperty] public partial string Language { get; set; } = string.Empty;

    public ObservableCollection<IRecentItem> RecentItems { get; } = [];

    [JsonIgnore] public string NexusModUrl => "https://www.nexusmods.com/baldursgate3/mods/22713";

    [ObservableProperty] public partial int MaxPreviewLines { get; set; } = 500;

    [ObservableProperty] public partial string DefaultOpenDirectory { get; set; } = string.Empty;

    [ObservableProperty] public partial string DefaultExportDirectory { get; set; } = string.Empty;
}