using System.Windows;
using BG3PakViewer.Dialogs.Views;
using iNKORE.UI.WPF.Modern.Controls.Primitives;
using Microsoft.Xaml.Behaviors;

namespace BG3PakViewer.Dialogs.Behaviors;

/// <summary>
///     Behavior for the log dialog.
/// </summary>
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

    /// <summary>
    ///     Set the regions for the custom title bar.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AssociatedObjectOnLoaded(object sender, RoutedEventArgs e)
    {
        if (TitleBar.GetExtendViewIntoTitleBar(AssociatedObject)) SetRegionsForCustomTitleBar();
    }

    /// <summary>
    ///     Set the regions for the custom title bar.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AssociatedObjectOnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (TitleBar.GetExtendViewIntoTitleBar(AssociatedObject)) SetRegionsForCustomTitleBar();
    }

    /// <summary>
    ///     Set the regions for the custom title bar.
    /// </summary>
    private void SetRegionsForCustomTitleBar()
    {
        AssociatedObject.RightPaddingColumn.Width =
            new GridLength(TitleBar.GetSystemOverlayRightInset(AssociatedObject));
    }
}