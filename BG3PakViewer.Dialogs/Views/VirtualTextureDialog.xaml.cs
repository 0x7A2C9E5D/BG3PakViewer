using System.Windows;
using System.Windows.Controls;
using BG3PakViewer.Dialogs.ViewModels;

namespace BG3PakViewer.Dialogs.Views;

/// <summary>
///     The cross-file virtual texture toolbox dialog.
/// </summary>
public partial class VirtualTextureDialog
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="VirtualTextureDialog" /> class.
    /// </summary>
    public VirtualTextureDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    ///     Shows the initial search text the dialog was opened with in the search box.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is VirtualTextureDialogViewModel viewModel)
            SearchBox.Text = viewModel.InitialSearch ?? string.Empty;
    }

    /// <summary>
    ///     Keeps the export selection in sync with the list, which is the list the export writes.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TextureList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is VirtualTextureDialogViewModel viewModel)
            viewModel.UpdateSelection(TextureList.SelectedItems.OfType<VirtualTextureItemViewModel>());
    }
}
