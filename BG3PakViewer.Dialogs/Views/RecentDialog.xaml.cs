using iNKORE.UI.WPF.Modern.Controls;
using Serilog;

namespace BG3PakViewer.Dialogs.Views;

public partial class RecentDialog
{
    public RecentDialog()
    {
        InitializeComponent();
        SetupSearchHelper();
    }

    private void SetupSearchHelper()
    {
        SfDataGrid.SearchHelper.AllowFiltering = true;
        SfDataGrid.SearchHelper.AllowCaseSensitiveSearch = false;
        SfDataGrid.SearchHelper.CanHighlightSearchText = false;
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.QueryText)) return;
        SfDataGrid.SearchHelper.Search(args.QueryText);
        Log.Information("Search query submitted: {QueryText}", args.QueryText);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(sender.Text))
            SfDataGrid.SearchHelper.ClearSearch();
    }
}