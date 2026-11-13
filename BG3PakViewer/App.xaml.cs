using System.Globalization;
using System.IO;
using System.Reactive;
using System.Reflection;
using System.Windows;
using BG3PakViewer.Contracts;
using BG3PakViewer.Dialogs.ViewModels;
using BG3PakViewer.Dialogs.Views;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Miscellaneous;
using BG3PakViewer.Models;
using BG3PakViewer.Services;
using BG3PakViewer.Services.ExportStrategies;
using BG3PakViewer.Services.PreviewHandlers;
using BG3PakViewer.ViewModels;
using BG3PakViewer.Views;
using BG3PakViewer.VirtualTextures.Catalog;
using BG3PakViewer.VirtualTextures.Extraction;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Syncfusion.Licensing;
using DebugHelper = BG3PakViewer.Miscellaneous.DebugHelper;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace BG3PakViewer;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public sealed partial class App : IDisposable
{
    private bool _disposedValue;
    private ObserverBase<LogEvent>? _logObserver;
    private SingleInstanceManager? _singleInstanceManager;

    /// <summary>
    ///     Releases the main window view model, the log observer and the single instance manager.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
    }

    /// <summary>
    ///     Configure services
    /// </summary>
    private static void ConfigureServices()
    {
        Ioc.Default.ConfigureServices(new ServiceCollection()
            .AddSingleton<PackageLoader>()
            .AddSingleton<ICultureMatcher, CultureMatcher>()
            .AddSingleton<ICultureResolver, CultureResolver>()
            .AddSingleton<ILogAccessService, LogAccessService>()
            .AddSingleton<ISettingsPersistenceService, SettingsPersistenceService>()
            // Loaded once here, so every consumer of IAppSettings writes back to the object that is
            // persisted on exit.
            .AddSingleton<IAppSettings, AppSettings>(x => x
                .GetRequiredService<ISettingsPersistenceService>().Load<AppSettings>())
            .AddSingleton<IShellOpenService, ShellOpenService>()
            .AddSingleton<IClipboardService, ClipboardService>()
            .AddSingleton<ISettingsManagerService, SettingsManagerService>()
            .AddSingleton<IPackageService, PackageService>()
            // Seeded with the recent list the settings object has already loaded.
            .AddSingleton<IRecentFilesService>(x => new RecentFilesService(
                x.GetRequiredService<IAppSettings>().RecentItems))
            .AddSingleton<IAppDiagnostics, AppDiagnostics>()
            .AddSingleton<IViewLocator, StrongViewLocator>(_ => CreateViewLocator())
            .AddSingleton<IDialogManager, DialogManager>()
            .AddSingleton<IDialogService, DialogService>()
            .AddSingleton<ICheckUpdateService, CheckUpdateService>()
            .AddLogging(b => b.AddSerilog())
            // Every strategy and preview handler needs its own registration: ExportService and
            // PreviewService receive them as an IEnumerable and dispatch to the first one that accepts
            // the entry.
            .AddSingleton<IExportStrategy, ImageExportStrategy>()
            .AddSingleton<IExportStrategy, Model3DExportStrategy>()
            .AddSingleton<IExportStrategy, AudioExportStrategy>()
            .AddSingleton<IExportStrategy, LocalizationExportStrategy>()
            .AddSingleton<IExportStrategy, LarianResourceExportStrategy>()
            .AddSingleton<IExportStrategy, VirtualTextureExportStrategy>()
            .AddSingleton<IPreviewHandler, PlainTextPreviewHandler>()
            .AddSingleton<IPreviewHandler, LarianResourcePreviewHandler>()
            .AddSingleton<IPreviewHandler, LocalizationPreviewHandler>()
            .AddSingleton<IPreviewHandler, ImagePreviewHandler>()
            .AddSingleton<IPreviewHandler, Model3DPreviewHandler>()
            .AddSingleton<IPreviewHandler, StoryScriptsPreviewHandler>()
            .AddSingleton<IPreviewHandler, VirtualTexturePreviewHandler>()
            .AddSingleton<IPreviewService, PreviewService>()
            .AddSingleton<IExportService, ExportService>()
            .AddSingleton<IPreviewExportService, PreviewExportService>()
            // Both hold streams of the loaded package, so the shell clears them before it replaces the
            // package.
            .AddSingleton<IVirtualTextureCatalog, VirtualTextureCatalog>()
            .AddSingleton<IVirtualTextureExtractor, VirtualTextureExtractor>()
            // Weak references, so a view that forgets to unregister is still collected.
            .AddSingleton<IMessenger, WeakReferenceMessenger>()
            .AddSingleton<MainWindowViewModel>()
            .BuildServiceProvider());
    }

    /// <summary>
    ///     Create view locator
    /// </summary>
    /// <returns></returns>
    private static StrongViewLocator CreateViewLocator()
    {
        // The dialog manager finds the view of a dialog through this locator, so a dialog view model it
        // can open has to be registered here.
        var viewLocator = new StrongViewLocator();
        viewLocator.Register<LogDialogViewModel, LogDialog>();
        viewLocator.Register<AboutDialogViewModel, AboutDialog>();
        viewLocator.Register<RecentDialogViewModel, RecentDialog>();
        viewLocator.Register<SettingsDialogViewModel, SettingsDialog>();
        viewLocator.Register<VirtualTextureToolboxDialogViewModel, VirtualTextureToolboxDialog>();
        return viewLocator;
    }

    /// <summary>
    ///     Initialize main window
    /// </summary>
    private static void InitializeMainWindow()
    {
        // The view model comes from the container, so the window shares its services with everything
        // else. Showing this window makes it the application's main window, and the default shutdown
        // mode ends the application with it.
        var mainWindow = new MainWindow
        {
            DataContext = Ioc.Default.GetRequiredService<MainWindowViewModel>()
        };
        mainWindow.Show();
    }

    /// <summary>
    ///     Initialize application
    /// </summary>
    private void InitializeApplication()
    {
        // The order matters: the container has to exist before the first service is resolved, the
        // exception handlers need the logger, and both have to be in place before the window is created.
        ConfigureServices();
        InitializeLogging();
        SetupExceptionHandling();
        InitializeCulture();
        RegisterSyncfusionLicense();
        InitializeSettingsManager();
        LogStartupInformation();
        InitializeMainWindow();
    }

    /// <summary>
    ///     Log startup information
    /// </summary>
    private static void LogStartupInformation()
    {
        Ioc.Default.GetRequiredService<IAppDiagnostics>().LogStartupInfo();
    }

    /// <summary>
    ///     Initialize settings manager
    /// </summary>
    private static void InitializeSettingsManager()
    {
        _ = Ioc.Default.GetRequiredService<ISettingsManagerService>();
    }

    /// <summary>
    ///     Initialize logging
    /// </summary>
    private void InitializeLogging()
    {
        // Replaces the logger created before startup with the same file sink plus the in-app log view;
        // events logged before this point are in the file only.
        _logObserver = new AnonymousObserver<LogEvent>(Ioc.Default.GetRequiredService<ILogAccessService>().Logs.Add);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(AppPaths.LogDirectory, "log.txt"),
                rollingInterval: RollingInterval.Day,
                formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.Observers(x => x.Subscribe(_logObserver))
            .CreateLogger();
    }

    /// <summary>
    ///     Initialize culture
    /// </summary>
    private static void InitializeCulture()
    {
        // An empty language means the settings have never been filled in: resolve the OS culture once and
        // store it, so the next start does not have to resolve it again.
        var appSettings = Ioc.Default.GetRequiredService<IAppSettings>();
        var language = appSettings.Language;
        CultureInfo cultureInfo;
        if (language == string.Empty)
        {
            cultureInfo = Ioc.Default.GetRequiredService<ICultureResolver>().ResolveSupportedCulture();
            appSettings.Language = cultureInfo.Name;
        }
        else
        {
            cultureInfo = new CultureInfo(language);
        }

        I18NExtension.Culture = cultureInfo;
    }

    /// <summary>
    ///     Register syncfusion license
    /// </summary>
    private static void RegisterSyncfusionLicense()
    {
        // The key is embedded in the assembly (EmbeddedResource in the project file), so a build has no
        // loose license file to keep next to the executable.
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("BG3PakViewer.Syncfusion.lic")!;
        using var reader = new StreamReader(stream);
        SyncfusionLicenseProvider.RegisterLicense(reader.ReadToEnd());
    }

    /// <summary>
    ///     Setup exception handling
    /// </summary>
    private void SetupExceptionHandling()
    {
        DispatcherUnhandledException += static (_, e) =>
        {
            // Logged and marked handled: one failing command should not close the window the user is
            // working in.
            e.Handled = true;
            var ex = e.Exception;
            Log.Error(ex, "Unhandled exception: {ExceptionMessage}", ex.Message);
        };

        TaskScheduler.UnobservedTaskException += static (_, e) =>
        {
            // Nothing awaits these tasks any more; logging them and marking them observed keeps the
            // failure from being reported a second time.
            e.SetObserved();
            var ex = e.Exception;
            Log.Error(ex, "Unobserved task exception: {ExceptionMessage}", ex.Message);
        };
    }

    /// <summary>
    ///     Checks whether another instance is already running, then initializes the application.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        // Set up logging before anything else: the full pipeline also feeds the in-app log view and
        // depends on the service container, so failures before that point would otherwise be lost.
        InitializeEarlyLogging();

        // Debug builds use their own mutex name, so a debug session can run next to an installed release
        // build instead of being treated as a second instance.
        _singleInstanceManager = new SingleInstanceManager(DebugHelper.IsDebug);
        if (_singleInstanceManager.IsAnotherInstanceRunning())
        {
            Log.Information("Another instance is already running; prompting the user");
            if (MessageBox.Show(Strings.MultipleInstanceMessage, Strings.MultipleInstanceCaption,
                    MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                _singleInstanceManager.ActivateExistingInstance();
            Log.Information("Shutting down this instance");
            Shutdown();
            return;
        }

        InitializeApplication();
    }

    /// <summary>
    ///     Initialize early logging
    /// </summary>
    private static void InitializeEarlyLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(AppPaths.LogDirectory, "log.txt"),
                rollingInterval: RollingInterval.Day,
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();
    }

    /// <summary>
    ///     Saves the settings and releases the resources the application owns.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        Ioc.Default.GetRequiredService<ISettingsPersistenceService>()
            .Save(Ioc.Default.GetRequiredService<IAppSettings>());
        Dispose();
    }

    /// <summary>
    ///     Releases the resources the application owns.
    /// </summary>
    /// <param name="disposing">Whether managed resources are released.</param>
    private void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            // The container is never disposed, so the window view model it keeps as a singleton is
            // released here by hand.
            (Current.MainWindow?.DataContext as IDisposable)?.Dispose();
            _logObserver?.Dispose();
            _singleInstanceManager?.Dispose();
        }

        _disposedValue = true;
    }
}
