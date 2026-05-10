using System.Collections.ObjectModel;
using BG3PakViewer.Contracts;
using Serilog.Events;

namespace BG3PakViewer.Services;

internal class LogAccessService : ILogAccessService
{
    public ObservableCollection<LogEvent> Logs { get; } = [];
}