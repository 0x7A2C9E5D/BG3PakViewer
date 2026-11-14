using System.Globalization;
using System.IO;
using System.Reactive;
using System.Reflection;
using System.Windows;
using BG3PakViewer.Contracts;
using BG3PakViewer.Dialogs.ViewModels;
using BG3PakViewer.Dialogs.Views;
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
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace BG3PakViewer;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public sealed partial class App : IDisposable
{
    private bool _disposedValue;
    private bool _initialized;
    private ObserverBase<LogEvent>? _logObserver;
    private ISingleInstanceManagerService? _singleInstanceManager;

    /// <summary>
    ///     Releases the log observer and the single instance manager.
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
            .AddSingleton<ICultureMatcher, CultureMatcher>()
            .AddSingleton<ICultureResolver, CultureResolver>()
            .AddSingleton<ILogAccessService, LogAccessService>()
            .AddSingleton<ISettingsPersistenceService, SettingsPersistenceService>()
            .AddSingleton<IAppSettings, AppSettings>(x => x
                .GetRequiredService<ISettingsPersistenceService>().Load<AppSettings>())
            .AddSingleton<IShellOpenService, ShellOpenService>()
            .AddSingleton<IClipboardService, ClipboardService>()
            .AddSingleton<ISettingsManagerService, SettingsManagerService>()
            .AddSingleton<IPackageService, PackageService>()
            .AddSingleton<IRecentFilesService>(x => new RecentFilesService(
                x.GetRequiredService<IAppSettings>().RecentItems))
            .AddSingleton<IAppDiagnosticsService, AppDiagnosticsService>()
            .AddSingleton<ISingleInstanceManagerService, SingleInstanceManagerService>()
            .AddSingleton<IViewLocator, StrongViewLocator>(_ => CreateViewLocator())
            .AddSingleton<IDialogManager, DialogManager>()
            .AddSingleton<IDialogService, DialogService>()
            .AddSingleton<ICheckUpdateService, CheckUpdateService>()
            .AddLogging(b => b.AddSerilog())
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
            .AddSingleton<IVirtualTextureCatalog, VirtualTextureCatalog>()
            .AddSingleton<IVirtualTextureExtractor, VirtualTextureExtractor>()
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
        InitializeLogging();
        SetupExceptionHandling();
        InitializeCulture();
        RegisterSyncfusionLicense();
        InitializeSettingsManager();
        LogStartupInformation();
        InitializeMainWindow();
        // Set last: a failure before this point leaves the instance uninitialized, so OnExit must
        // not save the settings it never loaded.
        _initialized = true;
    }

    /// <summary>
    ///     Log startup information
    /// </summary>
    private static void LogStartupInformation()
    {
        Ioc.Default.GetRequiredService<IAppDiagnosticsService>().LogStartupInfo();
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
        // The early logger writes to the same file, and a file sink keeps its log file open for
        // itself: the sink of the pipeline below cannot open it and silently writes nothing, so
        // everything logged while the application runs would be lost. Closing the early logger
        // first flushes what it captured and hands the file over.
        Log.CloseAndFlush();

        _logObserver = new AnonymousObserver<LogEvent>(Ioc.Default.GetRequiredService<ILogAccessService>().Add);

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
        var appSettings = Ioc.Default.GetRequiredService<IAppSettings>();
        // A settings file can hold no language at all or one this build does not know (hand edit, or a
        // culture that disappeared with an OS update). Resolving it blindly used to throw here, which is
        // the startup path: the process then ended up without a window. Fall back to a supported culture
        // and store the effective name back, so the same broken value is not resolved again next time.
        var cultureInfo = ResolveConfiguredCulture(appSettings.Language);
        appSettings.Language = cultureInfo.Name;
        I18NExtension.Culture = cultureInfo;
    }

    /// <summary>
    ///     Resolves the culture for the language name stored in the settings
    /// </summary>
    /// <param name="language">The language name stored in the settings</param>
    /// <returns>The culture of that name, or the best supported culture when it is empty or unknown</returns>
    private static CultureInfo ResolveConfiguredCulture(string? language)
    {
        try
        {
            return string.IsNullOrWhiteSpace(language)
                ? Ioc.Default.GetRequiredService<ICultureResolver>().ResolveSupportedCulture()
                : CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException ex)
        {
            Log.Warning(ex, "Configured language {Language} is unknown; falling back", language);
            return Ioc.Default.GetRequiredService<ICultureResolver>().ResolveSupportedCulture();
        }
    }

    /// <summary>
    ///     Register syncfusion license
    /// </summary>
    private static void RegisterSyncfusionLicense()
    {
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
            e.Handled = true;
            var ex = e.Exception;
            Log.Error(ex, "Unhandled exception: {ExceptionMessage}", ex.Message);
        };

        TaskScheduler.UnobservedTaskException += static (_, e) =>
        {
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

        // The single instance gate is resolved from the container, so registration has to happen
        // first. Registering constructs no other singleton and loads no settings: those factories
        // are lazy. Keep the startup-failure report this call had inside StartApplication's guard.
        try
        {
            ConfigureServices();
        }
        catch (Exception ex)
        {
            ReportStartupFailure(ex);
            return;
        }

        if (TryRedirectToRunningInstance())
        {
            Shutdown();
            return;
        }

        // The main window releases its view model after it has closed, because stopping the virtual
        // texture index is asynchronous, so the shutdown waits for that instead of ending with the
        // last window (see MainWindow.OnClosed).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        StartApplication();
    }

    /// <summary>
    ///     Prompts the user when another instance is already running and activates the existing one
    ///     on request.
    /// </summary>
    /// <returns>True when this instance must shut down.</returns>
    private bool TryRedirectToRunningInstance()
    {
        _singleInstanceManager = Ioc.Default.GetRequiredService<ISingleInstanceManagerService>();
        if (!_singleInstanceManager.IsAnotherInstanceRunning()) return false;

        Log.Information("Another instance is already running; prompting the user");
        if (MessageBox.Show(Strings.MultipleInstanceMessage, Strings.MultipleInstanceCaption,
                MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            _singleInstanceManager.ActivateExistingInstance();
        Log.Information("Shutting down this instance");
        return true;
    }

    /// <summary>
    ///     Initializes the application, reporting a failure to the user instead of leaving a running
    ///     process without a window.
    /// </summary>
    private void StartApplication()
    {
        // The dispatcher exception handler installed by InitializeApplication swallows anything thrown
        // from it, so a startup failure has to be caught here: otherwise WPF keeps running the message
        // loop while InitializeMainWindow never ran, and the user is left with a windowless process.
        try
        {
            InitializeApplication();
        }
        catch (Exception ex)
        {
            ReportStartupFailure(ex);
        }
    }

    /// <summary>
    ///     Reports a startup failure and shuts the application down with a failing exit code.
    /// </summary>
    /// <param name="exception">The exception that stopped the startup.</param>
    private void ReportStartupFailure(Exception exception)
    {
        Log.Error(exception, "Application failed to start");
        // The message box is the only window on this path: with the default OnLastWindowClose, closing
        // it would start a shutdown with exit code 0, and the Shutdown(1) below would then hit the
        // "already shutting down" early return in CriticalShutdown and lose the exit code. Pin the mode
        // so this method owns the shutdown and the failing code is the one that reaches the process.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        MessageBox.Show(
            string.Format(CultureInfo.CurrentCulture, Strings.StartupFailedMessage, exception.Message),
            Strings.StartupFailedCaption, MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(1);
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
    ///     Saves the settings and releases the resources the application owns, but only when this
    ///     instance initialized the services. A second instance that shut itself down because another
    ///     one was already running never loaded the settings, and saving them would overwrite the state
    ///     of the instance that keeps running.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        if (_initialized)
        {
            Ioc.Default.GetRequiredService<ISettingsPersistenceService>()
                .Save(Ioc.Default.GetRequiredService<IAppSettings>());
            Dispose();
        }
        else
        {
            _singleInstanceManager?.Dispose();
        }

        // Both paths logged something (a second instance reports why it is leaving), and the file
        // sink keeps its handle open until it is closed: flush it here so nothing is lost and the
        // file is released before the process ends.
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    /// <summary>
    ///     Releases the log observer and the single instance manager; the main window view model is
    ///     released by the window itself, which can await the virtual texture index.
    /// </summary>
    /// <param name="disposing">Whether managed resources are released.</param>
    private void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            _logObserver?.Dispose();
            _singleInstanceManager?.Dispose();
        }

        _disposedValue = true;
    }
}
