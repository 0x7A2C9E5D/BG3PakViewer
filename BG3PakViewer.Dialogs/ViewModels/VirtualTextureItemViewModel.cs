using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.VirtualTextures.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     One virtual texture in the toolbox list, including the tile set file it comes from.
/// </summary>
/// <param name="entry">The indexed texture.</param>
/// <param name="clipboardService">The service used to copy the texture name.</param>
public partial class VirtualTextureItemViewModel(
    VirtualTextureEntry entry,
    IClipboardService clipboardService)
    : ObservableObject
{
    /// <summary>
    ///     The indexed texture.
    /// </summary>
    public VirtualTextureEntry Entry { get; } = entry;

    /// <summary>
    ///     The name of the texture.
    /// </summary>
    public string Name => Entry.name;

    /// <summary>
    ///     The name of the tile set the texture belongs to.
    /// </summary>
    public string Source => Path.GetFileName(Entry.path);

    /// <summary>
    ///     Whether the name has been copied to the clipboard.
    /// </summary>
    [ObservableProperty]
    public partial bool IsCopied { get; set; }

    /// <summary>
    ///     Copies the name of the texture to the clipboard.
    /// </summary>
    [RelayCommand]
    private async Task CopyNameAsync()
    {
        if (!clipboardService.TrySetText(Name)) return;

        IsCopied = true;
        await Task.Delay(1500);
        IsCopied = false;
    }
}
