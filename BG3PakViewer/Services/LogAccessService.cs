using BG3PakViewer.Contracts;
using Serilog.Events;

namespace BG3PakViewer.Services;

/// <summary>
///     Log access service
/// </summary>
internal sealed class LogAccessService : ILogAccessService
{
    /// <summary>
    ///     Maximum number of entries kept in memory.
    /// </summary>
    private const int MaxEntries = 5000;

    private readonly List<LogEvent> _logEvents = [];
    private readonly Lock _logEventsLock = new();
    private Action<LogEvent>? _logAdded;

    /// <summary>
    ///     Stores a log entry. Entries arrive from whichever thread wrote the log, so every access is
    ///     guarded; the oldest entries are dropped once the limit is reached.
    /// </summary>
    /// <param name="logEvent"></param>
    public void Add(LogEvent logEvent)
    {
        lock (_logEventsLock)
        {
            _logEvents.Add(logEvent);
            if (_logEvents.Count > MaxEntries) _logEvents.RemoveRange(0, _logEvents.Count - MaxEntries);
            // Raised while the lock is held so Subscribe is atomic: a subscriber sees the
            // entry either in its snapshot or as a callback, never both and never neither. Handlers
            // therefore must not call back into this service synchronously.
            _logAdded?.Invoke(logEvent);
        }
    }

    /// <summary>
    ///     Registers a handler for subsequent entries and returns the entries stored so far.
    /// </summary>
    /// <param name="handler"></param>
    /// <returns></returns>
    public IReadOnlyList<LogEvent> Subscribe(Action<LogEvent> handler)
    {
        lock (_logEventsLock)
        {
            _logAdded += handler;
            return [.. _logEvents];
        }
    }

    /// <summary>
    ///     Removes a registered handler.
    /// </summary>
    /// <param name="handler"></param>
    public void Unsubscribe(Action<LogEvent> handler)
    {
        lock (_logEventsLock)
        {
            _logAdded -= handler;
        }
    }
}