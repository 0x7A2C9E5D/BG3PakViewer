using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Shared.ViewModels;

public abstract class DisposableViewModel : ObservableObject, IDisposable
{
    private bool _disposedValue;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        _disposedValue = true;
    }
}