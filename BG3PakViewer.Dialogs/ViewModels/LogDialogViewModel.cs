using System.Collections.ObjectModel;
using System.Windows;
using BG3PakViewer.Contracts;
using BG3PakViewer.Dialogs.Models;
using BG3PakViewer.Miscellaneous;
using BG3PakViewer.Shared.ViewModels;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using Serilog.Events;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     LogDialogViewModel
/// </summary>
public sealed partial class LogDialogViewModel
    : DisposableViewModel, IModalDialogViewModel
{
    private readonly ILogAccessService _logAccessService;
    private readonly Action<LogEvent> _logAddedHandler;
    private readonly IShellOpenService _shellOpenService;

    /// <summary>
    ///     initialize
    /// </summary>
    /// <param name="logAccessService"></param>
    /// <param name="shellOpenService"></param>
    public LogDialogViewModel(ILogAccessService logAccessService, IShellOpenService shellOpenService)
    {
        _logAccessService = logAccessService;
        _shellOpenService = shellOpenService;
        _logAddedHandler = OnLogAdded;
        var entries = logAccessService.Subscribe(_logAddedHandler);
        LogEvents = [.. entries.Select(x => new LogEventItemModel(x))];
    }

    /// <summary>
    ///     Log events.
    /// </summary>
    public ObservableCollection<LogEventItemModel> LogEvents { get; }

    /// <summary>
    ///     Dialog result.
    /// </summary>
    public bool? DialogResult => true;

    /// <summary>
    ///     Open log folder.
    /// </summary>
    [RelayCommand]
    private void OpenLogFolder()
    {
        _shellOpenService.Open(AppPaths.LogDirectory);
    }

    /// <summary>
    ///     Appends an entry written after the dialog opened.
    /// </summary>
    /// <param name="logEvent"></param>
    private void OnLogAdded(LogEvent logEvent)
    {
        // Called on the logging thread while the service lock is held, so only queue the work for the UI
        // thread: the collection itself is never touched from two threads.
        Application.Current.Dispatcher.BeginInvoke(() => LogEvents.Add(new LogEventItemModel(logEvent)));
    }

    /// <summary>
    ///     Unsubscribes from the log service.
    /// </summary>
    /// <param name="disposing">Whether managed resources are released.</param>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _logAccessService.Unsubscribe(_logAddedHandler);
    }
}
