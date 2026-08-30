using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using LSLib.VirtualTextures;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>List item describing a single virtual texture inside a GTS.</summary>
public class GtsTextureItemViewModel(FourCCTextureMeta meta) : ObservableObject
{
    public FourCCTextureMeta Meta { get; } = meta;

    public string DisplayName => Path.GetFileName(Meta.Name);

    public string Dimensions => $"{Meta.Width} × {Meta.Height}";
}
