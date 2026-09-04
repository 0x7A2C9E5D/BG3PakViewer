using iNKORE.UI.WPF.Modern.Controls;

namespace BG3PakViewer.Dialogs.Views;

/// <summary>
///     Recent dialog.
/// </summary>
public partial class RecentDialog
{
    /// <summary>
    ///     initialize
    /// </summary>
    public RecentDialog()
    {
        InitializeComponent();
        SetupSearchHelper();
    }

    /// <summary>
    ///     Setup search helper.
    /// </summary>
    private void SetupSearchHelper()
    {
        SfDataGrid.SearchHelper.AllowFiltering = true;
        SfDataGrid.SearchHelper.AllowCaseSensitiveSearch = false;
        SfDataGrid.SearchHelper.CanHighlightSearchText = false;
    }

    /// <summary>
    ///     Search box query submitted.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.QueryText)) return;
        SfDataGrid.SearchHelper.Search(args.QueryText);
    }

    /// <summary>
    ///     Search box text changed.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(sender.Text))
            SfDataGrid.SearchHelper.ClearSearch();
    }
}