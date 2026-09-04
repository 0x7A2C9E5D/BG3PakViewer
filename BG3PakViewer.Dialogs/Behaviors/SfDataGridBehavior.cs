using System.Windows;
using Microsoft.Xaml.Behaviors;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.UI.Xaml.Grid.Helpers;

namespace BG3PakViewer.Dialogs.Behaviors;

/// <summary>
///     Behavior for the SfDataGrid.
/// </summary>
internal class SfDataGridBehavior : Behavior<SfDataGrid>
{
    /// <summary>
    ///     The minimum height of the rows.
    /// </summary>
    public static readonly DependencyProperty MinHeightProperty
        = DependencyProperty.Register(nameof(MinHeight), typeof(double), typeof(SfDataGridBehavior),
            new PropertyMetadata(25.0));

    private readonly GridRowSizingOptions _gridRowResizingOptions = new();

    /// <summary>
    ///     The minimum height of the rows.
    /// </summary>
    public double MinHeight
    {
        get => (double)GetValue(MinHeightProperty);
        set => SetValue(MinHeightProperty, value);
    }

    protected override void OnAttached()
    {
        AssociatedObject.Unloaded += AssociatedObject_Unloaded;
        AssociatedObject.SizeChanged += AssociatedObject_SizeChanged;
        AssociatedObject.QueryRowHeight += AssociatedObject_QueryRowHeight;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
        AssociatedObject.SizeChanged -= AssociatedObject_SizeChanged;
        AssociatedObject.QueryRowHeight -= AssociatedObject_QueryRowHeight;
    }

    /// <summary>
    ///     Dispose the SfDataGrid.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AssociatedObject_Unloaded(object sender, RoutedEventArgs e)
    {
        AssociatedObject.SearchHelper.Dispose();
        AssociatedObject.Dispose();
    }

    /// <summary>
    ///     Reset the row height manager and invalidate the measure info.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AssociatedObject_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        AssociatedObject.GetVisualContainer().RowHeightManager.Reset();
        AssociatedObject.GetVisualContainer().InvalidateMeasureInfo();
    }

    /// <summary>
    ///     Set the row height to the minimum height if the auto height is less than the minimum height.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AssociatedObject_QueryRowHeight(object? sender, QueryRowHeightEventArgs e)
    {
        if (!AssociatedObject.GridColumnSizer.GetAutoRowHeight(e.RowIndex, _gridRowResizingOptions,
                out var autoHeight) || autoHeight <= MinHeight) return;
        e.Height = autoHeight;
        e.Handled = true;
    }
}