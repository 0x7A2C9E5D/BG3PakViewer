using System.IO;
using BG3PakViewer.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using LSLib.VirtualTextures;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>List item describing a single virtual texture inside a title set.</summary>
public partial class VirtualTextureItemViewModel(FourCCTextureMeta meta) : ObservableObject
{
    public FourCCTextureMeta Meta { get; } = meta;

    public string DisplayName => Path.GetFileName(Meta.Name);

    public string Dimensions => $"{Meta.Width} × {Meta.Height}";

    [ObservableProperty] public partial bool IsCopied { get; set; }

    /// <summary>
    ///     The clipboard is a platform capability, so it is reached through a service rather
    ///     than being called directly. It is resolved from the container instead of being
    ///     injected, which keeps the intermediate view models that create this item free of
    ///     a dependency they do not use themselves.
    /// </summary>
    private static IClipboardService ClipboardService => Ioc.Default.GetRequiredService<IClipboardService>();

    [RelayCommand]
    private async Task CopyNameAsync()
    {
        if (!ClipboardService.TrySetText(DisplayName)) return;

        IsCopied = true;
        await Task.Delay(1500);
        IsCopied = false;
    }
}