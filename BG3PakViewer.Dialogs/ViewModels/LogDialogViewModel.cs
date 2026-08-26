using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Data;
using BG3PakViewer.Contracts;
using BG3PakViewer.Dialogs.Models;
using BG3PakViewer.Miscellaneous;
using BG3PakViewer.Shared.ViewModels;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using Serilog.Events;

namespace BG3PakViewer.Dialogs.ViewModels;

public sealed partial class LogDialogViewModel
    : DisposableViewModel, IModalDialogViewModel
{
    private readonly IShellOpenService _shellOpenService;
    private readonly object _logEventsLock = new();
    private readonly ObservableCollection<LogEvent> _sourceLogEvents;

    public LogDialogViewModel(ILogAccessService logAccessService, IShellOpenService shellOpenService)
    {
        _shellOpenService = shellOpenService;
        _sourceLogEvents = logAccessService.Logs;
        BindingOperations.EnableCollectionSynchronization(LogEvents, _logEventsLock);
        LogEvents.CollectionChanged += OnLogEventsCollectionChanged;
        _sourceLogEvents.CollectionChanged += OnSourceLogsCollectionChanged;
        InitializeExistingLogs();
    }

    public ObservableCollection<LogEventItemModel> LogEvents { get; } = [];

    public bool? DialogResult => true;

    private void InitializeExistingLogs()
    {
        lock (_logEventsLock)
        {
            var models = _sourceLogEvents
                .Select(x => new LogEventItemModel(x)).ToList();
            foreach (var model in models)
                LogEvents.Add(model);
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        _shellOpenService.Open(AppPaths.LogDirectory);
    }

    private void OnSourceLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e is not { Action: NotifyCollectionChangedAction.Add, NewItems: not null }) return;
        foreach (LogEvent item in e.NewItems)
            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_logEventsLock)
                {
                    LogEvents.Add(new LogEventItemModel(item));
                }
            });
    }

    private void OnLogEventsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e is not { Action: NotifyCollectionChangedAction.Remove, OldItems: not null }) return;
        foreach (LogEventItemModel item in e.OldItems)
            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_logEventsLock)
                {
                    _sourceLogEvents.Remove(item.EventEntry);
                }
            });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        LogEvents.CollectionChanged -= OnLogEventsCollectionChanged;
        _sourceLogEvents.CollectionChanged -= OnSourceLogsCollectionChanged;
    }
}