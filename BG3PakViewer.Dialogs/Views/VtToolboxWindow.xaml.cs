using System.Windows;
using System.Windows.Controls;
using BG3PakViewer.Dialogs.ViewModels;

namespace BG3PakViewer.Dialogs.Views;

/// <summary>
///     The cross-file virtual texture toolbox window.
/// </summary>
public partial class VtToolboxWindow
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="VtToolboxWindow" /> class.
    /// </summary>
    public VtToolboxWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    ///     Shows the texture name the window was opened with in the search box.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is VtToolboxViewModel viewModel)
            SearchBox.Text = viewModel.InitialSearch ?? string.Empty;
    }

    /// <summary>
    ///     Keeps the export selection in sync with the list, which is the list the export writes.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TextureList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is VtToolboxViewModel viewModel)
            viewModel.UpdateSelection(TextureList.SelectedItems.OfType<VtToolboxTextureViewModel>());
    }
}
