using System.Windows;
using Microsoft.Xaml.Behaviors;
using Syncfusion.UI.Xaml.Controls.DataPager;

namespace BG3PakViewer.Controls.Behaviors;

/// <summary>
///     SfDataPager behavior
/// </summary>
internal class SfDataPagerBehavior : Behavior<SfDataPager>
{
    protected override void OnAttached()
    {
        AssociatedObject.Unloaded += AssociatedObject_Unloaded;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
    }

    /// <summary>
    ///     Dispose data pager
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AssociatedObject_Unloaded(object sender, RoutedEventArgs e)
    {
        AssociatedObject.Dispose();
    }
}