using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace BG3PakViewer.Dialogs.Behaviors;

/// <summary>
///     Guards a window against being closed while its owner is busy, for example while an export is
///     still writing files.
/// </summary>
/// <remarks>
///     The modern window style draws its own caption buttons, so no single API covers every close
///     path. The behavior disables the themed CloseButton and its
///     <see cref="SystemCommands.CloseWindowCommand" />, cancels any
///     <see cref="Window.Closing" /> event (where WPF turns Alt+F4 into a close), and also disables
///     SC_CLOSE and swallows WM_SYSCOMMAND for the native window. Application shutdown is exempted
///     so the process can still exit.
/// </remarks>
public sealed class WindowCloseGuardBehavior : Behavior<Window>
{
    /// <summary>
    ///     Whether the window may be closed. A view model binds to this attached property on the
    ///     window; the behavior attached to that same window applies every guard when it changes.
    /// </summary>
    public static readonly DependencyProperty CanCloseProperty = DependencyProperty.RegisterAttached(
        "CanClose", typeof(bool), typeof(WindowCloseGuardBehavior),
        new PropertyMetadata(true, OnCanCloseChanged));

    private const int WmSysCommand = 0x0112;

    private HwndSource? _hwndSource;
    private FrameworkElement? _closeButton;
    private CommandBinding? _closeCommandBinding;
    private bool _canClose = true;

    /// <summary>
    ///     Reads whether the window may be closed.
    /// </summary>
    /// <param name="window">The guarded window.</param>
    /// <returns><see langword="true" /> when closing is allowed.</returns>
    public static bool GetCanClose(Window window) => (bool)window.GetValue(CanCloseProperty);

    /// <summary>
    ///     Sets whether the window may be closed.
    /// </summary>
    /// <param name="window">The guarded window.</param>
    /// <param name="value"><see langword="true" /> when closing is allowed.</param>
    public static void SetCanClose(Window window, bool value) => window.SetValue(CanCloseProperty, value);

    /// <summary>
    ///     Subscribes to every close path and applies the state the attached property already holds.
    /// </summary>
    protected override void OnAttached()
    {
        base.OnAttached();

        var window = AssociatedObject;
        window.SourceInitialized += OnSourceInitialized;
        window.Loaded += OnLoaded;
        window.Closing += OnClosing;
        window.Closed += OnClosed;

        // Themed caption buttons execute this command instead of sending WM_SYSCOMMAND. Taking it
        // over both grays the button out through CanExecute and stops a click from closing us.
        _closeCommandBinding = new CommandBinding(SystemCommands.CloseWindowCommand,
            OnCloseWindowCommand, OnCanCloseWindowCommand);
        window.CommandBindings.Insert(0, _closeCommandBinding);

        CaptureWindowParts();
        Apply(GetCanClose(window));
    }

    /// <summary>
    ///     Removes every subscription and restores the close affordances before detaching.
    /// </summary>
    protected override void OnDetaching()
    {
        var window = AssociatedObject;
        window.SourceInitialized -= OnSourceInitialized;
        window.Loaded -= OnLoaded;
        window.Closing -= OnClosing;
        window.Closed -= OnClosed;

        if (_closeCommandBinding is not null)
            window.CommandBindings.Remove(_closeCommandBinding);

        Apply(true);
        _hwndSource?.RemoveHook(OnWindowMessage);
        _hwndSource = null;
        _closeButton = null;
        _closeCommandBinding = null;

        base.OnDetaching();
    }

    /// <summary>
    ///     Forwards a change of the attached property to every guard on that window.
    /// </summary>
    /// <param name="d">The window whose property changed.</param>
    /// <param name="e">The old and new values.</param>
    private static void OnCanCloseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window) return;

        foreach (var guard in Interaction.GetBehaviors(window).OfType<WindowCloseGuardBehavior>())
            guard.Apply((bool)e.NewValue);
    }

    /// <summary>
    ///     Captures the native handle and the themed Close button as soon as each becomes available.
    ///     The behavior may be attached before either exists, so this is also called on both window
    ///     lifetime events.
    /// </summary>
    private void CaptureWindowParts()
    {
        if (_hwndSource is null && PresentationSource.FromVisual(AssociatedObject) is HwndSource source)
        {
            _hwndSource = source;
            source.AddHook(OnWindowMessage);
        }

        if (_closeButton is null && AssociatedObject.IsLoaded)
            _closeButton = FindCloseButton(AssociatedObject);
    }

    /// <summary>
    ///     Looks for the template part the modern window style names "CloseButton".
    /// </summary>
    /// <param name="root">The visual subtree to search.</param>
    /// <returns>The close button, or <see langword="null" /> when the theme does not provide one.</returns>
    private static FrameworkElement? FindCloseButton(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement { Name: "CloseButton" } button) return button;
            var nested = FindCloseButton(child);
            if (nested is not null) return nested;
        }

        return null;
    }

    /// <summary>
    ///     Reapplies the current state after the native handle or the window template shows up.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        CaptureWindowParts();
        Apply(_canClose);
    }

    /// <summary>
    ///     Reapplies the current state once the window template has been loaded.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CaptureWindowParts();
        Apply(_canClose);
    }

    /// <summary>
    ///     Cancels a close while closing is blocked. WPF raises this event for Alt+F4, the themed X
    ///     button and every programmatic Close, so no close path can bypass it; shutting down the
    ///     application closes owned windows after the main window is gone and is allowed through.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_canClose && Application.Current.MainWindow is { IsLoaded: true })
            e.Cancel = true;
    }

    /// <summary>
    ///     Drops the native hook once the window goes away.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnClosed(object? sender, EventArgs e)
    {
        _hwndSource?.RemoveHook(OnWindowMessage);
        _hwndSource = null;
        _closeButton = null;
    }

    /// <summary>
    ///     Disables or restores every close affordance at once.
    /// </summary>
    /// <param name="canClose">Whether closing is allowed.</param>
    private void Apply(bool canClose)
    {
        _canClose = canClose;

        if (_closeButton is not null)
        {
            // Only force a local value while blocking; clearing it afterwards lets the theme's own
            // Availability binding decide again instead of leaving a stale IsEnabled behind.
            if (canClose)
                _closeButton.ClearValue(UIElement.IsEnabledProperty);
            else
                _closeButton.IsEnabled = false;
        }

        CommandManager.InvalidateRequerySuggested();

        if (_hwndSource is null) return;

        var menu = PInvoke.GetSystemMenu(new HWND(_hwndSource.Handle), false);
        if (menu.IsNull) return;

        PInvoke.EnableMenuItem(menu, PInvoke.SC_CLOSE,
            MENU_ITEM_FLAGS.MF_BYCOMMAND |
            (canClose ? MENU_ITEM_FLAGS.MF_ENABLED : MENU_ITEM_FLAGS.MF_GRAYED));
    }

    /// <summary>
    ///     Makes the themed Close button unavailable while closing is blocked, which is what grays
    ///     it out and keeps its click from reaching the window.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnCanCloseWindowCommand(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _canClose;
        e.Handled = true;
    }

    /// <summary>
    ///     Closes the window unless closing is blocked.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnCloseWindowCommand(object sender, ExecutedRoutedEventArgs e)
    {
        if (_canClose) AssociatedObject.Close();
        e.Handled = true;
    }

    /// <summary>
    ///     Swallows WM_SYSCOMMAND/SC_CLOSE, the native message behind the system menu's Close item,
    ///     while closing is blocked.
    /// </summary>
    /// <param name="hwnd">The window handle.</param>
    /// <param name="msg">The window message.</param>
    /// <param name="wParam">The message's first parameter.</param>
    /// <param name="lParam">The message's second parameter.</param>
    /// <param name="handled">Set to <see langword="true" /> when the message must not reach Windows.</param>
    /// <returns>The message result; always zero.</returns>
    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmSysCommand && !_canClose && (uint)(wParam.ToInt64() & 0xFFF0) == PInvoke.SC_CLOSE)
            handled = true;
        return IntPtr.Zero;
    }
}
