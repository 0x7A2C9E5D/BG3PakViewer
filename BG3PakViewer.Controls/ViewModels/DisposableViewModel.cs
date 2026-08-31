using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     Base class for view models that own disposable resources. Implements the
///     standard <see cref="IDisposable" /> / <see cref="IAsyncDisposable" /> pattern
///     (<c>Dispose(bool)</c> + <c>DisposeAsyncCore</c>). No finalizer is declared
///     because these view models only hold managed resources.
/// </summary>
/// <remarks>
///     Derived types clean up resources by overriding exactly one of:
///     <list type="bullet">
///         <item>
///             <see cref="Dispose(bool)" /> for synchronous cleanup (the default
///             <see cref="DisposeAsyncCore" /> bridges the async path into it), or
///         </item>
///         <item>
///             <see cref="DisposeAsyncCore()" /> for true asynchronous cleanup
///             (synchronous <see cref="Dispose()" /> then performs no managed cleanup).
///         </item>
///     </list>
///     The <c>_disposed</c> guard lives in <see cref="Dispose(bool)" />, so either
///     entry point may be called any number of times and the two paths cannot
///     double-release.
/// </remarks>
public abstract class DisposableViewModel : ObservableObject, IDisposable, IAsyncDisposable
{
    private bool _disposed;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Releases resources. Called exactly once thanks to the <c>_disposed</c> guard.
    /// </summary>
    /// <param name="disposing">
    ///     <see langword="true" /> when called from <see cref="Dispose" /> (or via the
    ///     default <see cref="DisposeAsyncCore" />); <see langword="false" /> is reserved
    ///     for a finalizer path and must not touch managed resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (disposing)
        {
            // Release managed resources here.
        }
        // Release unmanaged resources here.
    }

    /// <summary>
    ///     Performs asynchronous cleanup. The default implementation bridges to
    ///     <see cref="Dispose(bool)" />, so types that only implement synchronous
    ///     cleanup still release correctly on the async path. Override this when
    ///     cleanup must actually be asynchronous.
    /// </summary>
    protected virtual ValueTask DisposeAsyncCore()
    {
        Dispose(true);
        return ValueTask.CompletedTask;
    }
}