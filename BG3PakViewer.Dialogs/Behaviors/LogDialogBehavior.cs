using System.Windows;
using BG3PakViewer.Dialogs.Views;
using iNKORE.UI.WPF.Modern.Controls.Primitives;
using Microsoft.Xaml.Behaviors;

namespace BG3PakViewer.Dialogs.Behaviors;

internal class LogDialogBehavior : Behavior<LogDialog>
{
    protected override void OnAttached()
    {
        AssociatedObject.Loaded += AssociatedObjectOnLoaded;
        AssociatedObject.SizeChanged += AssociatedObjectOnSizeChanged;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= AssociatedObjectOnLoaded;
        AssociatedObject.SizeChanged -= AssociatedObjectOnSizeChanged;
    }

    private void AssociatedObjectOnLoaded(object sender, RoutedEventArgs e)
    {
        if (TitleBar.GetExtendViewIntoTitleBar(AssociatedObject)) SetRegionsForCustomTitleBar();
    }

    private void AssociatedObjectOnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (TitleBar.GetExtendViewIntoTitleBar(AssociatedObject)) SetRegionsForCustomTitleBar();
    }

    private void SetRegionsForCustomTitleBar()
    {
        AssociatedObject.RightPaddingColumn.Width =
            new GridLength(TitleBar.GetSystemOverlayRightInset(AssociatedObject));
    }
}