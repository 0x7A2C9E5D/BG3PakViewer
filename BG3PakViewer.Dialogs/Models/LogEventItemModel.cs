using System.Globalization;
using Serilog.Events;

namespace BG3PakViewer.Dialogs.Models;

/// <summary>
///     Log event item model.
/// </summary>
/// <param name="LogEvent"></param>
public record LogEventItemModel(LogEvent LogEvent)
{
    /// <summary>
    ///     Log event.
    /// </summary>
    public LogEvent EventEntry => LogEvent;

    /// <summary>
    ///     Log event timestamp.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public DateTimeOffset Timestamp => LogEvent.Timestamp;

    /// <summary>
    ///     Log event level.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public LogEventLevel Level => LogEvent.Level;

    /// <summary>
    ///     Log event message.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public string Message => LogEvent.RenderMessage(CultureInfo.InvariantCulture);
}