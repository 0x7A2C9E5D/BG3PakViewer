using System.Collections.ObjectModel;
using BG3PakViewer.Contracts;
using Serilog.Events;

namespace BG3PakViewer.Services;

/// <summary>
///     Log access service
/// </summary>
internal class LogAccessService : ILogAccessService
{
    /// <summary>
    ///     Logs
    /// </summary>
    public ObservableCollection<LogEvent> Logs { get; } = [];
}