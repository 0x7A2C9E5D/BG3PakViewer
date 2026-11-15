using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Microsoft.Xaml.Behaviors;

namespace BG3PakViewer.Shared.Behaviors;

/// <summary>
///     Applies a view-model supplied predicate to an <see cref="ItemsControl" />'s collection view.
///     This keeps WPF's <see cref="ICollectionView" /> out of view models: they expose filtering as a
///     plain <see cref="Predicate{T}" /> and stay testable without a WPF context, while the view still
///     gets the native filtering behavior (no collection rebuild, virtualization and the current
///     selection are preserved). With <see cref="AutoSelectFirstItem" />, a selection that the
///     filter consumed is replaced by the first remaining item. The filter is reapplied when the
///     items source is replaced too, since that gives the control a new collection view.
/// </summary>
public class CollectionViewFilterBehavior : Behavior<ItemsControl>
{
    private DependencyPropertyDescriptor? _itemsSourceDescriptor;

    public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(
        nameof(Filter), typeof(Predicate<object>), typeof(CollectionViewFilterBehavior),
        new PropertyMetadata(OnFilterChanged));

    public static readonly DependencyProperty AutoSelectFirstItemProperty = DependencyProperty.Register(
        nameof(AutoSelectFirstItem), typeof(bool), typeof(CollectionViewFilterBehavior),
        new PropertyMetadata(false));

    /// <summary>
    ///     The filter to apply to the collection view.
    /// </summary>
    public Predicate<object>? Filter
    {
        get => (Predicate<object>?)GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    /// <summary>
    ///     Selects the first item that survives the filter when the filter consumes the current
    ///     selection, so searching keeps a selection instead of leaving the control empty-handed.
    ///     Only applies to <see cref="Selector" />s, and does nothing when the filter matches nothing.
    /// </summary>
    public bool AutoSelectFirstItem
    {
        get => (bool)GetValue(AutoSelectFirstItemProperty);
        set => SetValue(AutoSelectFirstItemProperty, value);
    }

    protected override void OnAttached()
    {
        _itemsSourceDescriptor = DependencyPropertyDescriptor.FromProperty(
            ItemsControl.ItemsSourceProperty, typeof(ItemsControl));
        _itemsSourceDescriptor?.AddValueChanged(AssociatedObject, OnItemsSourceChanged);
        if (AssociatedObject.IsLoaded)
            ApplyFilter();
        else
            AssociatedObject.Loaded += OnAssociatedObjectLoaded;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnAssociatedObjectLoaded;
        _itemsSourceDescriptor?.RemoveValueChanged(AssociatedObject, OnItemsSourceChanged);
        if (GetView() is not { } view) return;
        view.Filter = null;
        view.Refresh();
    }

    private void OnItemsSourceChanged(object? sender, EventArgs e) => ApplyFilter();

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
        SelectFirstVisibleItem();
    }

    /// <summary>
    ///     Selects the first visible item when the refresh dropped the previous selection, matching
    ///     the selection the control makes when it is first opened. The predicate decides whether
    ///     the selection survived, so a selection the filter filtered out is always replaced.
    /// </summary>
    private void SelectFirstVisibleItem()
    {
        if (!AutoSelectFirstItem || AssociatedObject is not Selector selector) return;
        if (selector.SelectedItem is { } current && (Filter is null || Filter(current))) return;
        if (selector.Items.Count == 0) return;

        selector.SelectedIndex = 0;

        // Virtualized panels do not bring a programmatically selected row into view.
        if (selector is ListBox { SelectedItem: { } selected } listBox)
            listBox.ScrollIntoView(selected);
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