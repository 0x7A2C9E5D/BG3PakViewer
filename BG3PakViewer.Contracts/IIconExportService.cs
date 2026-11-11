namespace BG3PakViewer.Contracts;

/// <summary>
///     Exports the icons of an atlas preview as image files.
/// </summary>
public interface IIconExportService
{
    /// <summary>
    ///     Asks the user for a target folder and writes the given icons there as PNG files.
    /// </summary>
    /// <param name="items">The icons to export, with the names of the files they are written to.</param>
    Task ExportAsync(IReadOnlyList<IconExportItem> items);
}