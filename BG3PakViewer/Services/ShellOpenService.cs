using System.Diagnostics;
using BG3PakViewer.Contracts;
using Serilog;

namespace BG3PakViewer.Services;

internal class ShellOpenService : IShellOpenService
{
    public void Open(string path)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            Log.Information("Opened external resource: {Path}", path);
        }
        catch (Exception ex)
        {
            // No handler is registered for the target or the path is gone; the click just does
            // nothing in the UI, so the log is the only trace left behind.
            Log.Error(ex, "Failed to open external resource: {Path}", path);
        }
    }
}