using System.Collections.ObjectModel;
using Serilog.Events;

namespace BG3PakViewer.Contracts;

public interface ILogAccessService
{
    public ObservableCollection<LogEvent> Logs { get; }
}