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

    public void Dispose()
    {
        Dispose(true);
    }

    private static void ConfigureServices()
    {
        Ioc.Default.ConfigureServices(new ServiceCollection()
            .AddSingleton<PackageLoader>()
            .AddSingleton<ICultureMatcher, CultureMatcher>()
            .AddSingleton<ICultureResolver, CultureResolver>()
            .AddSingleton<ILogAccessService, LogAccessService>()
            .AddSingleton<IConfigService, ConfigService>()
            .AddSingleton<IAppSettings, AppSettings>(x => x
                .GetRequiredService<IConfigService>().Load<AppSettings>())
            .AddSingleton<IExplorerService, ExplorerService>()
            .AddSingleton<ISettingsManagerService, SettingsManagerService>()
            .AddSingleton<IPackageService, PackageService>()
            .AddSingleton<IRecentFilesService>(x => new RecentFilesService(
                x.GetRequiredService<IAppSettings>().RecentItems))
            .AddSingleton<IAppDiagnostics, AppDiagnostics>()
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
            .AddSingleton<IPreviewHandler, OsirisScriptsPreviewHandler>()
            .AddSingleton<IPreviewService, PreviewService>()
            .AddSingleton<IExportService, ExportService>()
            .AddSingleton<IMessenger, WeakReferenceMessenger>()
            .AddSingleton<MainWindowViewModel>()
            .BuildServiceProvider());
    }

    private static StrongViewLocator CreateViewLocator()
    {
        var viewLocator = new StrongViewLocator();
        viewLocator.Register<LogDialogViewModel, LogDialog>();
        viewLocator.Register<AboutDialogViewModel, AboutDialog>();
        viewLocator.Register<RecentDialogViewModel, RecentDialog>();
        viewLocator.Register<SettingsDialogViewModel, SettingsDialog>();
        return viewLocator;
    }

    private static void InitializeMainWindow()
    {
        var mainWindow = new MainWindow
        {
            DataContext = Ioc.Default.GetRequiredService<MainWindowViewModel>()
        };
        mainWindow.Show();
    }

    private void InitializeApplication()
    {
        ConfigureServices();
        InitializeLogging();
        SetupExceptionHandling();
        InitializeCulture();
        RegisterSyncfusionLicense();
        InitializeSettingsManager();
        LogStartupInformation();
        InitializeMainWindow();
    }

    private static void LogStartupInformation()
    {
        Ioc.Default.GetRequiredService<IAppDiagnostics>().LogStartupInfo();
    }

    private static void InitializeSettingsManager()
    {
        _ = Ioc.Default.GetRequiredService<ISettingsManagerService>();
    }

    private void InitializeLogging()
    {
        _logObserver = new AnonymousObserver<LogEvent>(Ioc.Default.GetRequiredService<ILogAccessService>().Logs.Add);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(AppPaths.LogDirectory, "log.txt"),
                rollingInterval: RollingInterval.Day,
                formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.Observers(x => x.Subscribe(_logObserver))
            .CreateLogger();
    }

    private static void InitializeCulture()
    {
        var appSettings = Ioc.Default.GetRequiredService<IAppSettings>();
        var cultureInfo = appSettings.Language == string.Empty
            ? Ioc.Default.GetRequiredService<ICultureResolver>().ResolveSupportedCulture()
            : new CultureInfo(appSettings.Language);
        if (appSettings.Language == string.Empty)
            appSettings.Language = cultureInfo.Name;
        I18NExtension.Culture = cultureInfo;
    }

    private static void RegisterSyncfusionLicense()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("BG3PakViewer.License.txt")!;
        using var reader = new StreamReader(stream);
        SyncfusionLicenseProvider.RegisterLicense(reader.ReadToEnd());
    }

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
            e.SetObserved();
            var ex = e.Exception;
            Log.Error(ex, "Unobserved task exception: {ExceptionMessage}", ex.Message);
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceManager = new SingleInstanceManager(DebugHelper.IsDebug);
        if (_singleInstanceManager.IsAnotherInstanceRunning())
        {
            if (MessageBox.Show(Strings.MultipleInstanceMessage, Strings.MultipleInstanceCaption,
                    MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                _singleInstanceManager.ActivateExistingInstance();
            Shutdown();
            return;
        }

        InitializeApplication();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Ioc.Default.GetRequiredService<IConfigService>()
            .Save(Ioc.Default.GetRequiredService<IAppSettings>());
        Dispose();
    }

    private void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            (Current.MainWindow?.DataContext as IDisposable)?.Dispose();
            _logObserver?.Dispose();
            _singleInstanceManager?.Dispose();
        }

        _disposedValue = true;
    }
}