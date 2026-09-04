using System.Collections.ObjectModel;
using Serilog.Events;

namespace BG3PakViewer.Contracts;

/// <summary>
///     Log access service
/// </summary>
public interface ILogAccessService
{
    /// <summary>
    ///     Logs
    /// </summary>
    public ObservableCollection<LogEvent> Logs { get; }
}