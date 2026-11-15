using Serilog.Events;

namespace BG3PakViewer.Contracts;

/// <summary>
///     Log access service
/// </summary>
public interface ILogAccessService
{
    /// <summary>
    ///     Stores a log entry, keeping only the most recent entries.
    /// </summary>
    /// <param name="logEvent"></param>
    void Add(LogEvent logEvent);

    /// <summary>
    ///     Registers a handler for subsequent entries and returns the entries stored so far, as one
    ///     atomic step, so a caller can render the full history and then follow it live without missing
    ///     or repeating an entry.
    /// </summary>
    /// <param name="handler"></param>
    /// <returns></returns>
    IReadOnlyList<LogEvent> Subscribe(Action<LogEvent> handler);

    /// <summary>
    ///     Removes a handler registered by <see cref="Subscribe" />.
    /// </summary>
    /// <param name="handler"></param>
    void Unsubscribe(Action<LogEvent> handler);
}
