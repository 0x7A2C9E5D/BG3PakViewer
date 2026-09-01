using System.Windows;
using BG3PakViewer.Messaging;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Behaviors;
using Syncfusion.UI.Xaml.Grid;

namespace BG3PakViewer.Controls.Behaviors;

/// <summary>
///     Keeps the localization grid's <see cref="SfDataGrid.SearchHelper" /> in sync with
///     <see cref="SearchMessage" /> broadcasts: non-empty text performs a filtered search,
///     empty text clears it so the full content is restored.
/// </summary>
internal class SfDataGridBehavior : Behavior<SfDataGrid>
{
    protected override void OnAttached()
    {
        AssociatedObject.Unloaded += AssociatedObject_Unloaded;
        WeakReferenceMessenger.Default.Register<SearchMessage>(this, (_, message) => OnSearchMessage(message));
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
        WeakReferenceMessenger.Default.Unregister<SearchMessage>(this);
    }

    private void OnSearchMessage(SearchMessage message)
    {
        var text = message.Text;
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

    private void AssociatedObject_Unloaded(object sender, RoutedEventArgs e)
    {
        AssociatedObject.SearchHelper.Dispose();
        AssociatedObject.Dispose();
    }
}