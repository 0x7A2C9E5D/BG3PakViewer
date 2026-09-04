using System.IO;
using BG3PakViewer.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using LSLib.VirtualTextures;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for a single virtual texture.
/// </summary>
/// <param name="meta"></param>
public partial class VirtualTextureItemViewModel(FourCCTextureMeta meta) : ObservableObject
{
    /// <summary>
    ///     The virtual texture metadata.
    /// </summary>
    public FourCCTextureMeta Meta { get; } = meta;

    /// <summary>
    ///     The display name of the virtual texture.
    /// </summary>
    public string DisplayName => Path.GetFileName(Meta.Name);

    /// <summary>
    ///     The dimensions of the virtual texture.
    /// </summary>
    public string Dimensions => $"{Meta.Width} × {Meta.Height}";

    /// <summary>
    ///     Whether the name has been copied to the clipboard.
    /// </summary>
    [ObservableProperty] public partial bool IsCopied { get; set; }

    private static IClipboardService ClipboardService => Ioc.Default.GetRequiredService<IClipboardService>();

    /// <summary>
    ///     Copies the name of the virtual texture to the clipboard.
    /// </summary>
    [RelayCommand]
    private async Task CopyNameAsync()
    {
        if (!ClipboardService.TrySetText(DisplayName)) return;

        IsCopied = true;
        await Task.Delay(1500);
        IsCopied = false;
    }
}