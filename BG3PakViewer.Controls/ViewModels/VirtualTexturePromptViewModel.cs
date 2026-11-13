using CommunityToolkit.Mvvm.ComponentModel;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     Tells the user that virtual texture files are viewed and exported from the virtual texture
///     preview export tool.
/// </summary>
/// <param name="fileName">The name of the selected file.</param>
public sealed class VirtualTexturePromptViewModel(string fileName) : ObservableObject
{
    /// <summary>
    ///     The name of the selected file.
    /// </summary>
    public string FileName { get; } = fileName;
}
