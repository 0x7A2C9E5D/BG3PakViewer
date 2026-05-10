using System.Windows;
using BG3PakViewer.Messaging;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Xaml.Behaviors;
using Serilog;

namespace BG3PakViewer.Controls.Behaviors;

internal class Viewport3DxBehavior : Behavior<Viewport3DX>
{
    private readonly IMessenger _messenger = Ioc.Default.GetRequiredService<IMessenger>();

    protected override void OnAttached()
    {
        AssociatedObject.Unloaded += AssociatedObject_Unloaded;
        _messenger.Register<ZoomExtentsMessage, string>(this, MessageTokens.ZoomExtents,
            (_, _) => { AssociatedObject.ZoomExtents(); });
        Log.Information("Viewport3DxBehavior.OnAttached");
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
        _messenger.UnregisterAll(this);
        Log.Information("Viewport3DxBehavior.OnDetaching");
    }

    private void AssociatedObject_Unloaded(object sender, RoutedEventArgs e)
    {
        AssociatedObject.EffectsManager?.Dispose();
        AssociatedObject.Dispose();
        Log.Information("Viewport3DxBehavior.AssociatedObject_Unloaded");
    }
}