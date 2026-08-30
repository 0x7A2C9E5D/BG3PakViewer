using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using LSLib.VirtualTextures;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>GTS 内单张虚拟纹理的清单条目。</summary>
public class GtsTextureItemViewModel(FourCCTextureMeta meta) : ObservableObject
{
    public FourCCTextureMeta Meta { get; } = meta;

    public string DisplayName => Path.GetFileName(Meta.Name);

    public string Dimensions => $"{Meta.Width} × {Meta.Height}";
}
