using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        try
        {
            Clipboard.SetText(DisplayName);
        }
        catch (Exception)
        {
            // Clipboard may be locked by another process; silently ignore.
        }

        IsCopied = true;
        await Task.Delay(1500);
        IsCopied = false;
    }
}