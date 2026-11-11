using System.Windows.Media.Imaging;

namespace BG3PakViewer.Contracts;

/// <summary>
///     An icon to export, together with the name of the file it is written to.
/// </summary>
/// <param name="Image">The bitmap of the icon.</param>
/// <param name="FileName">The name of the file, including its extension.</param>
public sealed record IconExportItem(BitmapSource Image, string FileName);