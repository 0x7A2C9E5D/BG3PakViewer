using System.Globalization;
using Serilog.Events;

namespace BG3PakViewer.Dialogs.Models;

public record LogEventItemModel(LogEvent LogEvent)
{
    public LogEvent EventEntry => LogEvent;

    // ReSharper disable once UnusedMember.Global
    public DateTimeOffset Timestamp => LogEvent.Timestamp;

    // ReSharper disable once UnusedMember.Global
    public LogEventLevel Level => LogEvent.Level;

    // ReSharper disable once UnusedMember.Global
    public string Message => LogEvent.RenderMessage(CultureInfo.InvariantCulture);
}