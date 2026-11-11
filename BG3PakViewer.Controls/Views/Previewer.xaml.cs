using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BG3PakViewer.Controls.Contracts;

namespace BG3PakViewer.Controls.Views;

/// <summary>
///     Previewer.xaml 的交互逻辑
/// </summary>
public partial class Previewer
{
    /// <summary>
    ///     initialize
    /// </summary>
    public Previewer()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Forwards the key event to the shown preview, which decides whether it handles the key.
    /// </summary>
    /// <param name="e">The key event forwarded by the window.</param>
    /// <returns>Whether the key was handled by the preview.</returns>
    public bool HandleKeyDown(KeyEventArgs e)
    {
        // Text inputs keep their own Ctrl+A, so the shortcuts only apply outside of them.
        if (Keyboard.FocusedElement is TextBoxBase)
            return false;

        return PreviewContent.Content is IPreviewKeyHandler handler && handler.HandleKeyDown(e);
    }
}