using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using BG3PakViewer.Dialogs.ViewModels;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace BG3PakViewer.Dialogs.Views;

/// <summary>
///     The cross-file virtual texture toolbox dialog.
/// </summary>
/// <remarks>
///     The modern window style draws its own caption buttons, so disabling the native system menu
///     alone never reaches the Close button. While an export is running this window disables the
///     themed CloseButton and its <see cref="SystemCommands.CloseWindowCommand" />, cancels any
///     <see cref="Window.Closing" /> event (where WPF turns Alt+F4 into a close), and also disables
///     SC_CLOSE and swallows WM_SYSCOMMAND for the native window. All of it is restored as soon as
///     the export ends. Shutting down the application is exempted so the process can still exit.
/// </remarks>
public partial class VirtualTextureDialog
{
    private const int WmSysCommand = 0x0112;

    private HwndSource? _hwndSource;
    private VirtualTextureDialogViewModel? _viewModel;
    private FrameworkElement? _closeButton;
    private bool _exporting;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VirtualTextureDialog" /> class.
    /// </summary>
    public VirtualTextureDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;

        // Themed caption buttons execute this command instead of sending WM_SYSCOMMAND. Taking it
        // over both grays the button out through CanExecute and stops a click from closing us.
        CommandBindings.Insert(0, new CommandBinding(SystemCommands.CloseWindowCommand,
            OnCloseWindowCommand, OnCanCloseWindowCommand));
    }

    /// <summary>
    ///     Shows the initial search text the dialog was opened with in the search box, and starts
    ///     following the view model's export state.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = e.NewValue as VirtualTextureDialogViewModel;
        if (_viewModel is null) return;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SearchBox.Text = _viewModel.InitialSearch ?? string.Empty;
        SetExporting(_viewModel.IsExporting);
    }

    /// <summary>
    ///     Captures the native window handle once WPF has created it, then applies the export state
    ///     that may already have been recorded before the handle existed.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        _hwndSource?.AddHook(OnWindowMessage);
        SetExporting(_exporting);
    }

    /// <summary>
    ///     Finds the themed Close button in the window template, which only exists once the window
    ///     has been loaded, and applies the export state that may have been reached before that.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _closeButton = FindCloseButton(this);
        SetExporting(_exporting);
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
    ///     Cancels a close while an export is running. WPF raises this event for Alt+F4, the themed
    ///     X button and every programmatic Close, so no close path can bypass it; shutting down the
    ///     application closes owned windows after the main window is gone and is allowed through.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exporting && Application.Current.MainWindow is { IsLoaded: true })
            e.Cancel = true;
    }

    /// <summary>
    ///     Detaches the window hook and the view model subscription when the window goes away.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnClosed(object? sender, EventArgs e)
    {
        _hwndSource?.RemoveHook(OnWindowMessage);
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    /// <summary>
    ///     Follows <see cref="VirtualTextureDialogViewModel.IsExporting" /> so closing is disabled
    ///     for exactly the duration of an export and restored as soon as it ends.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VirtualTextureDialogViewModel.IsExporting) && _viewModel is not null)
            SetExporting(_viewModel.IsExporting);
    }

    /// <summary>
    ///     Disables every close affordance for the duration of an export: the themed button, its
    ///     command, and the native system menu item that Windows draws caption buttons from.
    /// </summary>
    /// <param name="exporting">Whether an export is running.</param>
    private void SetExporting(bool exporting)
    {
        _exporting = exporting;

        if (_closeButton is not null)
        {
            // Only force a local value while blocking; clearing it afterward lets the theme's own
            // Availability binding decide again instead of leaving a stale IsEnabled behind.
            if (exporting)
                _closeButton.IsEnabled = false;
            else
                _closeButton.ClearValue(UIElement.IsEnabledProperty);
        }

        CommandManager.InvalidateRequerySuggested();

        if (_hwndSource is null) return;

        var menu = PInvoke.GetSystemMenu(new HWND(_hwndSource.Handle), false);
        if (menu.IsNull) return;

        PInvoke.EnableMenuItem(menu, PInvoke.SC_CLOSE,
            MENU_ITEM_FLAGS.MF_BYCOMMAND |
            (exporting ? MENU_ITEM_FLAGS.MF_GRAYED : MENU_ITEM_FLAGS.MF_ENABLED));
    }

    /// <summary>
    ///     Makes the themed Close button report itself as unavailable while exporting, which is what
    ///     grays it out and keeps its click from reaching the window.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnCanCloseWindowCommand(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = !_exporting;
        e.Handled = true;
    }

    /// <summary>
    ///     Closes the window unless an export is running.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnCloseWindowCommand(object sender, ExecutedRoutedEventArgs e)
    {
        if (!_exporting) Close();
        e.Handled = true;
    }

    /// <summary>
    ///     Swallows WM_SYSCOMMAND/SC_CLOSE, the native message behind the system menu's Close item,
    ///     while an export is running.
    /// </summary>
    /// <param name="hwnd">The window handle.</param>
    /// <param name="msg">The window message.</param>
    /// <param name="wParam">The message's first parameter.</param>
    /// <param name="lParam">The message's second parameter.</param>
    /// <param name="handled">Set to <see langword="true" /> when the message must not reach Windows.</param>
    /// <returns>The message result; always zero.</returns>
    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmSysCommand && _exporting && (uint)(wParam.ToInt64() & 0xFFF0) == PInvoke.SC_CLOSE)
            handled = true;
        return IntPtr.Zero;
    }

    /// <summary>
    ///     Keeps the export selection in sync with the list, which is the list the export writes.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TextureList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is VirtualTextureDialogViewModel viewModel)
            viewModel.UpdateSelection(TextureList.SelectedItems.OfType<VirtualTextureItemViewModel>());
    }
}
