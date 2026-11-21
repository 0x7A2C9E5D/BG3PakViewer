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
}
