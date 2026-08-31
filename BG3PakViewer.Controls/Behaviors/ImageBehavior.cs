using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Xaml.Behaviors;

namespace BG3PakViewer.Controls.Behaviors;

internal class ImageBehavior : Behavior<Image>
{
    protected override void OnAttached()
    {
        AssociatedObject.Loaded += AssociatedObjectOnLoaded;
        AssociatedObject.SizeChanged += AssociatedObjectOnSizeChanged;
        AssociatedObject.TargetUpdated += AssociatedObjectOnTargetUpdated;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= AssociatedObjectOnLoaded;
        AssociatedObject.SizeChanged -= AssociatedObjectOnSizeChanged;
        AssociatedObject.TargetUpdated -= AssociatedObjectOnTargetUpdated;
    }

    private void AssociatedObjectOnLoaded(object sender, RoutedEventArgs e)
    {
        if (AssociatedObject.Source is null) return;
        UpdateImageStretch();
    }

    private void AssociatedObjectOnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Fires after every layout pass, so it both applies the decision once the
        // arranged size is known and heals any decision made from a stale size.
        UpdateImageStretch();
    }

    private void AssociatedObjectOnTargetUpdated(object? sender, DataTransferEventArgs e)
    {
        if (e.Property != Image.SourceProperty
            || AssociatedObject.Source is null) return;

        // The binding pushes the new Source before layout runs, so ActualWidth/Height
        // still reflect the previous image (with Stretch=None they even follow the
        // image's own size). Defer the decision until the layout pass has arranged
        // the image; SizeChanged above acts as a fallback that heals stale guesses.
        AssociatedObject.Dispatcher.BeginInvoke(DispatcherPriority.Render, UpdateImageStretch);
    }

    private void UpdateImageStretch()
    {
        var source = AssociatedObject.Source;
        if (source is null) return;

        var desired = source.Width > AssociatedObject.ActualWidth ||
                      source.Height > AssociatedObject.ActualHeight
            ? Stretch.Uniform
            : Stretch.None;

        // Assign only when it changes to avoid re-triggering layout feedback loops.
        if (AssociatedObject.Stretch != desired)
            AssociatedObject.Stretch = desired;
    }
}