using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Shared.ViewModels;

/// <summary>
///     DisposableViewModel
/// </summary>
public abstract class DisposableViewModel : ObservableObject, IDisposable
{
    private bool _disposedValue;

    /// <summary>
    ///     Releases the resources of the view model.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Releases the resources of the view model; derived types override this and call the base.
    /// </summary>
    /// <param name="disposing">Whether managed resources are released.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        _disposedValue = true;
    }
}