using iNKORE.UI.WPF.Modern.Controls;

namespace BG3PakViewer.Dialogs.Views;

/// <summary>
///     LogDialog.xaml 的交互逻辑
/// </summary>
public partial class LogDialog
{
    /// <summary>
    ///     initialize
    /// </summary>
    public LogDialog()
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
    private void AutoSuggestBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.QueryText)) return;
        SfDataGrid.SearchHelper.Search(args.QueryText);
    }

    /// <summary>
    ///     Search box text changed.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void AutoSuggestBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(sender.Text))
            SfDataGrid.SearchHelper.ClearSearch();
    }
}