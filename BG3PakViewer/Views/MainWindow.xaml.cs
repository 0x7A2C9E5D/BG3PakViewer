using System.Windows;
using iNKORE.UI.WPF.Modern;

namespace BG3PakViewer.Views;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    /// <summary>
    ///     Initializes a new instance of the MainWindow class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Theme switch button on click
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ThemeSwitchBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.Current.ApplicationTheme =
            ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Light
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
    }

    /// <summary>
    ///     Releases the view model once the window is closed, then shuts the application down.
    /// </summary>
    /// <remarks>
    ///     The release waits for the virtual texture index to stop reading the package, so it cannot run
    ///     inside <c>Closing</c> without blocking the UI thread. The application therefore runs in
    ///     explicit shutdown mode (see <see cref="App.OnStartup" />) and is shut down here instead of
    ///     ending with the window.
    /// </remarks>
    /// <param name="e">The event data.</param>
    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        try
        {
            if (DataContext is IAsyncDisposable disposable) await disposable.DisposeAsync();
        }
        finally
        {
            Application.Current.Shutdown();
        }
    }
}