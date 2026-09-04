using System.ComponentModel;
using System.Windows;
using BG3PakViewer.Locales;
using BG3PakViewer.ViewModels;
using BG3PakViewer.Views;
using CommunityToolkit.Mvvm.Messaging;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using iNKORE.UI.WPF.Modern.Controls.Primitives;
using Microsoft.Xaml.Behaviors;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace BG3PakViewer.Behaviors;

/// <summary>
///     MainWindow behavior
/// </summary>
internal class MainWindowBehavior : Behavior<MainWindow>
{
    protected override void OnAttached()
    {
        AssociatedObject.Closed += AssociatedObject_Closed;
        AssociatedObject.Loaded += AssociatedObjectOnLoaded;
        AssociatedObject.Closing += AssociatedObject_Closing;
        AssociatedObject.SizeChanged += AssociatedObjectOnSizeChanged;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Closed -= AssociatedObject_Closed;
        AssociatedObject.Loaded -= AssociatedObjectOnLoaded;
        AssociatedObject.Closing -= AssociatedObject_Closing;
        AssociatedObject.SizeChanged -= AssociatedObjectOnSizeChanged;
    }

    /// <summary>
    ///     Unregister all messages
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AssociatedObject_Closed(object? sender, EventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(AssociatedObject);
        WeakReferenceMessenger.Default.UnregisterAll((MainWindowViewModel)AssociatedObject.DataContext);
    }

    /// <summary>
    ///     Set regions for custom title bar
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AssociatedObjectOnLoaded(object sender, RoutedEventArgs e)
    {
        if (TitleBar.GetExtendViewIntoTitleBar(AssociatedObject)) SetRegionsForCustomTitleBar();
    }

    /// <summary>
    ///     Set regions for custom title bar
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AssociatedObjectOnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (TitleBar.GetExtendViewIntoTitleBar(AssociatedObject)) SetRegionsForCustomTitleBar();
    }

    /// <summary>
    ///     Set regions for custom title bar
    /// </summary>
    private void SetRegionsForCustomTitleBar()
    {
        AssociatedObject.RightPaddingColumn.Width =
            new GridLength(TitleBar.GetSystemOverlayRightInset(AssociatedObject));
    }

    /// <summary>
    ///     Cancel closing when exporting
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AssociatedObject_Closing(object? sender, EventArgs e)
    {
        if (((MainWindowViewModel)AssociatedObject.DataContext).IsExporting && MessageBox.Show(AssociatedObject,
                Strings.WhenIsExportingExitMessage, Strings.IsExporting, MessageBoxButton.OKCancel,
                SegoeFluentIcons.Warning) == MessageBoxResult.Cancel) ((CancelEventArgs)e).Cancel = true;
    }
}