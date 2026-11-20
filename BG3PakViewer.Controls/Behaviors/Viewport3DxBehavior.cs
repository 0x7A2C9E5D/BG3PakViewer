using System.Windows;
using BG3PakViewer.Messaging;
using CommunityToolkit.Mvvm.Messaging;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Xaml.Behaviors;

namespace BG3PakViewer.Controls.Behaviors;

/// <summary>
///     Viewport3DX behavior
/// </summary>
internal class Viewport3DxBehavior : Behavior<Viewport3DX>, IRecipient<string>
{
    private bool _disposed;

    /// <summary>
    ///     Zooms the viewport to its extents when <see cref="MessageTokens.ZoomExtents" /> is broadcast.
    /// </summary>
    /// <param name="message"></param>
    public void Receive(string message)
    {
        AssociatedObject.ZoomExtents();
    }

    protected override void OnAttached()
    {
        AssociatedObject.Unloaded += AssociatedObject_Unloaded;
        WeakReferenceMessenger.Default.Register<string, string>(this, MessageTokens.ZoomExtents);
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    /// <summary>
    ///     Releases the viewport
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AssociatedObject_Unloaded(object sender, RoutedEventArgs e)
    {
        // Unloading does not detach the behavior, so without this the broadcast would keep reaching a
        // disposed viewport. Unloaded can also fire more than once, so the disposal must be idempotent.
        AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        if (_disposed) return;
        _disposed = true;
        AssociatedObject.EffectsManager?.Dispose();
        AssociatedObject.Dispose();
    }
}
