using System.ComponentModel;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     The single place where a state change is translated into property and command notifications.
/// </summary>
public partial class StoryScriptsPreviewViewModel
{
    private void OnStateChanged(StoryScriptsStateChange change)
    {
        if ((change & StoryScriptsStateChange.Preview) != 0)
            OnPropertyChanged(nameof(IsPreviewing));

        if ((change & StoryScriptsStateChange.Search) != 0)
            ExportSearchResultsCommand.NotifyCanExecuteChanged();

        if ((change & StoryScriptsStateChange.Selection) != 0)
            ExportSelectedCommand.NotifyCanExecuteChanged();

        if ((change & StoryScriptsStateChange.Export) == 0) return;
        ExportAllCommand.NotifyCanExecuteChanged();
        ExportSelectedCommand.NotifyCanExecuteChanged();
        ExportSearchResultsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Keeps the search region in sync with the filter published by the base view model.
    /// </summary>
    /// <param name="e">The property that changed.</param>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(ItemFilter))
            _state.SetSearchFiltered(ItemFilter is not null);
    }
}
