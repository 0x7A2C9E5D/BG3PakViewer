using System.IO;
using BG3PakViewer.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LSLib.VirtualTextures;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>List item describing a single virtual texture inside a GTS.</summary>
public partial class GtsTextureItemViewModel(FourCCTextureMeta meta) : ObservableObject
{
    public FourCCTextureMeta Meta { get; } = meta;

    public string DisplayName => Path.GetFileName(Meta.Name);

    public string Dimensions => $"{Meta.Width} × {Meta.Height}";

    [ObservableProperty] public partial bool IsCopied { get; set; }

    [RelayCommand]
    private async Task CopyNameAsync()
    {
        // Request the copy from the UI layer: the clipboard is a platform capability, and
        // routing it through the messenger keeps this view model free of UI dependencies.
        var copied = await WeakReferenceMessenger.Default.Send(
            new AsyncRequestMessage<string, bool>(DisplayName), MessageTokens.CopyToClipboard);
        if (!copied) return;

        IsCopied = true;
        await Task.Delay(1500);
        IsCopied = false;
    }
}
