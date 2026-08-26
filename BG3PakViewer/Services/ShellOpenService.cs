using System.Diagnostics;
using BG3PakViewer.Contracts;

namespace BG3PakViewer.Services;

internal class ShellOpenService : IShellOpenService
{
    public void Open(string path)
    {
        using var _ = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}