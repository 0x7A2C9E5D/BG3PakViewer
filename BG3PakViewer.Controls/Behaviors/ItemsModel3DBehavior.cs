using System.Windows;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Xaml.Behaviors;

namespace BG3PakViewer.Controls.Behaviors;

/// <summary>
///     ItemsModel3D behavior
/// </summary>
internal class ItemsModel3DBehavior : Behavior<ItemsModel3D>
{
    protected override void OnAttached()
    {
        AssociatedObject.Unloaded += AssociatedObjectOnUnloaded;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Unloaded -= AssociatedObjectOnUnloaded;
    }

    /// <summary>
    ///     Clear children
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AssociatedObjectOnUnloaded(object sender, RoutedEventArgs e)
    {
        AssociatedObject.Children.Clear();
    }
}