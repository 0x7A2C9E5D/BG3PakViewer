using System.Windows.Input;

namespace BG3PakViewer.Controls.Contracts;

/// <summary>
///     A preview view model that handles keyboard shortcuts itself.
/// </summary>
public interface IPreviewKeyHandler
{
    /// <summary>
    ///     Handles a key event forwarded by the previewer.
    /// </summary>
    /// <param name="e">The key event.</param>
    /// <returns>Whether the key was handled by the preview.</returns>
    bool HandleKeyDown(KeyEventArgs e);
}