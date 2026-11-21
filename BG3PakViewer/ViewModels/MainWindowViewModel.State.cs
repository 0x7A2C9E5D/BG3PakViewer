using BG3PakViewer.Shared.Models;

namespace BG3PakViewer.ViewModels;

/// <summary>
///     The single place where a state change is translated into property and command notifications.
/// </summary>
internal partial class MainWindowViewModel
{
    private void OnStateChanged(MainWindowStateChange change)
    {
        if ((change & MainWindowStateChange.Selection) != 0)
            ExportCommand.NotifyCanExecuteChanged();

        if ((change & MainWindowStateChange.Export) == 0) return;
        OnPropertyChanged(nameof(IsExporting));
        ExportCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Keeps the selection region in sync with the tree selection.
    /// </summary>
    /// <param name="value">The newly selected tree item.</param>
    partial void OnSelectedItemChanged(PackageEntry? value)
    {
        _state.SetSelection(value is not null);
    }
}
