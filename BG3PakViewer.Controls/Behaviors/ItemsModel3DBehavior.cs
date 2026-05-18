using System.Windows;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Xaml.Behaviors;

namespace BG3PakViewer.Controls.Behaviors;

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

    private void AssociatedObjectOnUnloaded(object sender, RoutedEventArgs e)
    {
        AssociatedObject.Children.Clear();
    }
}