using System.ComponentModel;
using BG3PakViewer.Dialogs.ViewModels.States;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     The single place where a state change is translated into property and command notifications.
/// </summary>
public partial class VirtualTextureDialogViewModel
{
    private void OnStateChanged(VirtualTextureStateChange change)
    {
        if ((change & VirtualTextureStateChange.Index) != 0)
        {
            OnPropertyChanged(nameof(IsIndexing));
            OnPropertyChanged(nameof(IsBusy));
            ExportAllCommand.NotifyCanExecuteChanged();
            ExportSelectedCommand.NotifyCanExecuteChanged();
            ExportSearchResultsCommand.NotifyCanExecuteChanged();
        }

        if ((change & VirtualTextureStateChange.Search) != 0)
            ExportSearchResultsCommand.NotifyCanExecuteChanged();

        if ((change & VirtualTextureStateChange.Preview) != 0)
        {
            OnPropertyChanged(nameof(IsPreviewing));
            OnPropertyChanged(nameof(IsBusy));
        }

        if ((change & VirtualTextureStateChange.Selection) != 0)
            ExportSelectedCommand.NotifyCanExecuteChanged();

        if ((change & VirtualTextureStateChange.Export) == 0) return;
        OnPropertyChanged(nameof(IsExporting));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanClose));
        OnPropertyChanged(nameof(ExportButtonText));
        ExportAllCommand.NotifyCanExecuteChanged();
        ExportSelectedCommand.NotifyCanExecuteChanged();
        ExportSearchResultsCommand.NotifyCanExecuteChanged();
        CancelExportCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Keeps the search region in sync with the filter published by the base view model, and makes
    ///     the search-results command depend on both the filter and the indexed list.
    /// </summary>
    /// <param name="e">The property that changed.</param>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(ItemFilter):
                _state.SetSearchFiltered(ItemFilter is not null);
                break;
            case nameof(Textures):
                ExportSearchResultsCommand.NotifyCanExecuteChanged();
                break;
        }
    }
}
