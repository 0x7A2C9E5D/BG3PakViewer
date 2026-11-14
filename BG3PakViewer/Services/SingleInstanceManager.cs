using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using BG3PakViewer.Miscellaneous;
using CommunityToolkit.Diagnostics;
using Serilog;
using WINDOWPLACEMENT = Windows.Win32.UI.WindowsAndMessaging.WINDOWPLACEMENT;

namespace BG3PakViewer.Services;

/// <summary>
///     Single instance manager
/// </summary>
internal sealed class SingleInstanceManager : ISingleInstanceManager
{
    private readonly string _mutexName = DebugHelper.IsDebug ? "BG3PakViewer_Debug" : "BG3PakViewer";
    private bool _disposedValue;
    private Mutex? _mutex;

    /// <summary>
    ///     Releases the single instance mutex.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Is another instance running
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ObjectDisposedException"></exception>
    public bool IsAnotherInstanceRunning()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        _mutex = new Mutex(true, _mutexName, out var createdNew);
        if (createdNew) return false;

        Log.Information("Another instance is already running (mutex: {MutexName})", _mutexName);
        return true;
    }

    /// <summary>
    ///     Activate existing instance
    /// </summary>
    /// <exception cref="ObjectDisposedException"></exception>
    public void ActivateExistingInstance()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        var mainWindowHandle = FindExistingInstanceWindow();
        if (mainWindowHandle == IntPtr.Zero)
        {
            Log.Warning("Another instance was detected, but no window of it could be found");
            return;
        }

        ActivateExistingInstanceWindow(new HWND(mainWindowHandle));
    }

    /// <summary>
    ///     Finds the main window of the other running instance.
    /// </summary>
    /// <remarks>
    ///     Every process of the enumeration is disposed, because only the window handle is needed
    ///     afterward and each <see cref="Process" /> holds a handle of its own until it is disposed. A
    ///     process that merely shares the executable name is not the running instance: when its executable
    ///     path differs from this one it is skipped. A process whose path cannot be read (for instance one
    ///     that runs with higher rights) still counts, because the name already matched.
    /// </remarks>
    /// <returns>The window handle of the other instance, or <see cref="IntPtr.Zero" /> when it has none.</returns>
    private static IntPtr FindExistingInstanceWindow()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var currentPath = GetExecutablePath(currentProcess);
        var processes = Process.GetProcessesByName(currentProcess.ProcessName);
        try
        {
            foreach (var process in processes)
            {
                if (process.Id == currentProcess.Id) continue;
                var path = GetExecutablePath(process);
                if (currentPath is not null && path is not null &&
                    !string.Equals(currentPath, path, StringComparison.OrdinalIgnoreCase))
                    continue;

                var mainWindowHandle = process.MainWindowHandle;
                if (mainWindowHandle != IntPtr.Zero) return mainWindowHandle;
            }

            return IntPtr.Zero;
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    /// <summary>
    ///     Reads the path of the executable a process was started from.
    /// </summary>
    /// <param name="process">The process to read.</param>
    /// <returns>The executable path, or <see langword="null" /> when it cannot be read.</returns>
    private static string? GetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Win32Exception)
        {
            // A process that runs with higher rights does not expose its modules.
            return null;
        }
        catch (InvalidOperationException)
        {
            // The process exited while it was being inspected.
            return null;
        }
    }

    /// <summary>
    ///     Activate existing instance window
    /// </summary>
    /// <param name="mainWindowHandle"></param>
    private static void ActivateExistingInstanceWindow(HWND mainWindowHandle)
    {
        var placement = new WINDOWPLACEMENT();
        placement.length = (uint)Marshal.SizeOf(placement);
        if (PInvoke.GetWindowPlacement(mainWindowHandle, ref placement).Value == 0)
        {
            Log.Warning("Could not query the window placement of the running instance (handle: {Handle})",
                mainWindowHandle.ToHexString());
            return;
        }

        if (placement.showCmd == SHOW_WINDOW_CMD.SW_SHOWMINIMIZED)
            PInvoke.ShowWindow(mainWindowHandle, SHOW_WINDOW_CMD.SW_RESTORE);
        PInvoke.SetForegroundWindow(mainWindowHandle);
    }

    /// <summary>
    ///     Releases the mutex; managed resources only when called from <see cref="Dispose()" />.
    /// </summary>
    /// <param name="disposing">Whether managed resources are released.</param>
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

    /// <summary>
    ///     Finalizer that releases the mutex when <see cref="Dispose()" /> was not called.
    /// </summary>
    ~SingleInstanceManager()
    {
        Dispose(false);
    }
}
