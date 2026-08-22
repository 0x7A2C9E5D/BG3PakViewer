using System.Collections.ObjectModel;
using LSLib.LS;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing Larian localization files (.loca) in a table.
///     Each row maps a localization key to its text, with the version column.
/// </summary>
public class LocalizationPreviewViewModel
{
    public ObservableCollection<LocalizationRowViewModel> Rows { get; } = [];

    public static LocalizationPreviewViewModel FromResource(LocaResource resource)
    {
        var viewModel = new LocalizationPreviewViewModel();
        foreach (var entry in resource.Entries)
        {
            viewModel.Rows.Add(new LocalizationRowViewModel
            {
                Key = entry.Key,
                Version = entry.Version,
                Text = entry.Text
            });
        }

        return viewModel;
    }
}

/// <summary>
///     A single localization entry rendered as a row in the preview table.
/// </summary>
public class LocalizationRowViewModel
{
    public required string Key { get; init; }

    public required ushort Version { get; init; }

    public required string Text { get; init; }
}
