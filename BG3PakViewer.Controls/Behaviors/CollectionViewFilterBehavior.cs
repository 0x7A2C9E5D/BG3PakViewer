using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Xaml.Behaviors;

namespace BG3PakViewer.Controls.Behaviors;

/// <summary>
///     Applies a view-model supplied predicate to an <see cref="ItemsControl" />'s collection view.
///     This keeps WPF's <see cref="ICollectionView" /> out of view models: they expose filtering as a
///     plain <see cref="Predicate{T}" /> and stay testable without a WPF context, while the view still
///     gets the native filtering behavior (no collection rebuild, virtualization and the current
///     selection are preserved).
/// </summary>
internal class CollectionViewFilterBehavior : Behavior<ItemsControl>
{
    public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(
        nameof(Filter), typeof(Predicate<object>), typeof(CollectionViewFilterBehavior),
        new PropertyMetadata(OnFilterChanged));

    /// <summary>
    ///     The filter to apply to the collection view.
    /// </summary>
    public Predicate<object>? Filter
    {
        get => (Predicate<object>?)GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    protected override void OnAttached()
    {
        if (AssociatedObject.IsLoaded)
            ApplyFilter();
        else
            AssociatedObject.Loaded += OnAssociatedObjectLoaded;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnAssociatedObjectLoaded;
        if (GetView() is not { } view) return;
        view.Filter = null;
        view.Refresh();
    }

    /// <summary>
    ///     Applies the filter to the collection view.
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private static void OnFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var behavior = (CollectionViewFilterBehavior)d;
        if (behavior.AssociatedObject is { IsLoaded: true }) behavior.ApplyFilter();
    }

    /// <summary>
    ///     Applies the filter to the collection view.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnAssociatedObjectLoaded(object sender, RoutedEventArgs e)
    {
        AssociatedObject.Loaded -= OnAssociatedObjectLoaded;
        ApplyFilter();
    }

    /// <summary>
    ///     Applies the filter to the collection view.
    /// </summary>
    private void ApplyFilter()
    {
        if (GetView() is not { } view) return;

        view.Filter = Filter;
        view.Refresh();
    }

    /// <summary>
    ///     The view WPF itself binds the control to. Resolving it from <see cref="ItemsControl.ItemsSource" />
    ///     yields the same instance the control uses, so setting the filter here affects what is displayed.
    /// </summary>
    private ICollectionView? GetView()
    {
        return AssociatedObject.ItemsSource is { } source ? CollectionViewSource.GetDefaultView(source) : null;
    }
}