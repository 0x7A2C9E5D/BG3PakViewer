using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace BG3PakViewer.Controls.Behaviors;

internal class ImageBehavior : Behavior<Image>
{
    protected override void OnAttached()
    {
        AssociatedObject.Loaded += AssociatedObjectOnLoaded;
        AssociatedObject.TargetUpdated += AssociatedObjectOnTargetUpdated;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= AssociatedObjectOnLoaded;
        AssociatedObject.TargetUpdated -= AssociatedObjectOnTargetUpdated;
    }

    private void AssociatedObjectOnLoaded(object sender, RoutedEventArgs e)
    {
        if (AssociatedObject.Source is null) return;
        UpdateImageStretch();
    }

    private void AssociatedObjectOnTargetUpdated(object? sender, DataTransferEventArgs e)
    {
        if (e.Property != Image.SourceProperty
            || AssociatedObject.Source is null) return;
        UpdateImageStretch();
    }

    private void UpdateImageStretch()
    {
        AssociatedObject.Stretch =
            AssociatedObject.Source.Width > AssociatedObject.ActualWidth ||
            AssociatedObject.Source.Height > AssociatedObject.ActualHeight
                ? Stretch.Uniform
                : Stretch.None;
    }
}