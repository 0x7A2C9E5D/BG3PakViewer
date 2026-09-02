using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using CommunityToolkit.Diagnostics;

namespace BG3PakViewer.Services;

internal sealed class SingleInstanceManager(bool isDebug) : IDisposable
{
    private readonly string _mutexName = isDebug ? "BG3PakViewer_Debug" : "BG3PakViewer";
    private bool _disposedValue;
    private Mutex? _mutex;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public bool IsAnotherInstanceRunning()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        _mutex = new Mutex(true, _mutexName, out var createdNew);
        if (createdNew) return false;

        Log.Information("Another instance is already running (mutex: {MutexName}).", _mutexName);
        return true;
    }

    public void ActivateExistingInstance()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        using var existingProcess = FindExistingProcessInstance();
        if (existingProcess == null)
        {
            Log.Warning("Another instance was detected, but its process could not be found.");
            return;
        }

        var mainWindowHandle = new HWND(existingProcess.MainWindowHandle);
        ActivateExistingInstanceWindow(mainWindowHandle);
    }

    private static Process? FindExistingProcessInstance()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var processes = Process.GetProcessesByName(currentProcess.ProcessName);
        return processes.FirstOrDefault(p => p.Id != currentProcess.Id);
    }

    private static void ActivateExistingInstanceWindow(HWND mainWindowHandle)
    {
        var placement = new WINDOWPLACEMENT();
        placement.length = (uint)Marshal.SizeOf(placement);
        if (PInvoke.GetWindowPlacement(mainWindowHandle, ref placement).Value == 0)
        {
            Log.Warning("Could not query the window placement of the running instance (handle: {Handle}).",
                mainWindowHandle.ToHexString());
            return;
        }
        if (placement.showCmd == SHOW_WINDOW_CMD.SW_SHOWMINIMIZED)
            PInvoke.ShowWindow(mainWindowHandle, SHOW_WINDOW_CMD.SW_RESTORE);
        PInvoke.SetForegroundWindow(mainWindowHandle);
    }

    private void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            _mutex?.Dispose();
            _mutex = null;
        }

        _disposedValue = true;
    }

    ~SingleInstanceManager()
    {
        Dispose(false);
    }
}