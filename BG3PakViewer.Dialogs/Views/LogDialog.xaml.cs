using iNKORE.UI.WPF.Modern.Controls;
using Serilog;

namespace BG3PakViewer.Dialogs.Views;

/// <summary>
///     LogDialog.xaml 的交互逻辑
/// </summary>
public partial class LogDialog
{
    public LogDialog()
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

    private void AutoSuggestBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.QueryText)) return;
        SfDataGrid.SearchHelper.Search(args.QueryText);
        Log.Information("Search query submitted: {QueryText}", args.QueryText);
    }

    private void AutoSuggestBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(sender.Text))
            SfDataGrid.SearchHelper.ClearSearch();
    }
}