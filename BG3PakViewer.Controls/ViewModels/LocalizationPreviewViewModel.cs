using System.Collections.ObjectModel;
using LSLib.LS;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing Larian localization files (.loca): exposes the resource's entries
///     (key / version / text), which the view renders as a table.
/// </summary>
public class LocalizationPreviewViewModel
{
    /// <summary>
    ///     The localization entries.
    /// </summary>
    public ObservableCollection<LocalizationEntryViewModel> Entries { get; } = [];
    
    /// <summary>
    ///     Builds a view model from a localization resource.
    /// </summary>
    /// <param name="resource"></param>
    /// <returns></returns>
    public static LocalizationPreviewViewModel FromResource(LocaResource resource)
    {
        var viewModel = new LocalizationPreviewViewModel();
        foreach (var entry in resource.Entries)
            viewModel.Entries.Add(new LocalizationEntryViewModel
            {
                Key = entry.Key,
                Version = entry.Version,
                Text = entry.Text
            });
        return viewModel;
    }
}

/// <summary>
///     A single localization entry: the resource key mapped to its text, with the version column.
/// </summary>
public class LocalizationEntryViewModel
{
    /// <summary>
    ///     The localization key.
    /// </summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public required string Key { get; init; }
    
    /// <summary>
    ///     The localization version.
    /// </summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public required ushort Version { get; init; }
        
    /// <summary>
    ///     The localization text.
    /// </summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public required string Text { get; init; }
}