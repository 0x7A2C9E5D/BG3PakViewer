using System.Windows;
using BG3PakViewer.Messaging;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Xaml.Behaviors;
using Syncfusion.UI.Xaml.Grid;

namespace BG3PakViewer.Shared.Behaviors;

/// <summary>
///     Keeps the localization grid's <see cref="SfDataGrid.SearchHelper" /> in sync with
///     <see cref="ValueChangedMessage{T}" /> broadcasts: non-empty text performs a filtered search,
///     empty text clears it so the full content is restored.
/// </summary>
public class SfDataGridSearchBehavior : Behavior<SfDataGrid>
{
    protected override void OnAttached()
    {
        AssociatedObject.Unloaded += AssociatedObject_Unloaded;
        AssociatedObject.SearchHelper.CanHighlightSearchText = false;
        WeakReferenceMessenger.Default.Register<ValueChangedMessage<string?>, string>(
            this, MessageTokens.SearchQueryChanged, (_, message) => OnSearchMessage(message));
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
        WeakReferenceMessenger.Default.Unregister<ValueChangedMessage<string?>, string>(this,
            MessageTokens.SearchQueryChanged);
    }

    /// <summary>
    ///     Handle search message
    /// </summary>
    /// <param name="message"></param>
    private void OnSearchMessage(ValueChangedMessage<string?> message)
    {
        var text = message.Value;
        if (string.IsNullOrEmpty(text))
        {
            AssociatedObject.SearchHelper.ClearSearch();
        }
        else
        {
            AssociatedObject.SearchHelper.AllowFiltering = true;
            AssociatedObject.SearchHelper.Search(text);
        }
    }

    /// <summary>
    ///     Dispose search helper
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AssociatedObject_Unloaded(object sender, RoutedEventArgs e)
    {
        AssociatedObject.SearchHelper.Dispose();
        AssociatedObject.Dispose();
    }
}