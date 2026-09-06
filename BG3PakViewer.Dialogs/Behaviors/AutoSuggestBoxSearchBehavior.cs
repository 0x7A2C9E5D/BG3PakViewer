using System.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Xaml.Behaviors;
using Syncfusion.UI.Xaml.Grid;

namespace BG3PakViewer.Dialogs.Behaviors;

/// <summary>
///     Wires an <see cref="AutoSuggestBox" /> search box to an <see cref="SfDataGrid" />'s
///     search helper: submitting a non-empty query searches the grid, clearing the box restores
///     the full content. Attach this behavior to the search box itself (not the grid) and point
///     <see cref="SearchGrid" /> at the grid to filter. Unlike
///     <see cref="BG3PakViewer.Shared.Behaviors.SfDataGridSearchBehavior" />, which is attached to a
///     grid and driven by search messages, this one is driven by the box's own events.
/// </summary>
internal sealed class AutoSuggestBoxSearchBehavior : Behavior<AutoSuggestBox>
{
    /// <summary>
    ///     Identifies the <see cref="SearchGrid" /> dependency property.
    /// </summary>
    public static readonly DependencyProperty SearchGridProperty = DependencyProperty.Register(
        nameof(SearchGrid),
        typeof(SfDataGrid),
        typeof(AutoSuggestBoxSearchBehavior),
        new PropertyMetadata(null, static (d, _) => ((AutoSuggestBoxSearchBehavior)d).ConfigureSearchHelper()));

    /// <summary>
    ///     The grid whose content the box filters.
    /// </summary>
    public SfDataGrid? SearchGrid
    {
        get => (SfDataGrid?)GetValue(SearchGridProperty);
        set => SetValue(SearchGridProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.QuerySubmitted += OnQuerySubmitted;
        AssociatedObject.TextChanged += OnTextChanged;
        ConfigureSearchHelper();
    }

    /// <inheritdoc />
    protected override void OnDetaching()
    {
        AssociatedObject.QuerySubmitted -= OnQuerySubmitted;
        AssociatedObject.TextChanged -= OnTextChanged;
        base.OnDetaching();
    }

    /// <summary>
    ///     Applies the default grid search options once the search grid is known.
    /// </summary>
    private void ConfigureSearchHelper()
    {
        if (SearchGrid is null) return;
        SearchGrid.SearchHelper.AllowFiltering = true;
        SearchGrid.SearchHelper.AllowCaseSensitiveSearch = false;
        SearchGrid.SearchHelper.CanHighlightSearchText = false;
    }

    /// <summary>
    ///     Searches the search grid for the submitted query.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (SearchGrid is null || string.IsNullOrWhiteSpace(args.QueryText)) return;
        SearchGrid.SearchHelper.Search(args.QueryText);
    }

    /// <summary>
    ///     Clears the search grid's search when the box is emptied.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (SearchGrid is not null && string.IsNullOrEmpty(sender.Text))
            SearchGrid.SearchHelper.ClearSearch();
    }
}