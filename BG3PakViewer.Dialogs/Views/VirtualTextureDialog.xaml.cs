using System.Windows;
using System.Windows.Controls;
using BG3PakViewer.Dialogs.ViewModels;
using iNKORE.UI.WPF.Modern.Controls.Primitives;

namespace BG3PakViewer.Dialogs.Views;

/// <summary>
///     The cross-file virtual texture toolbox dialog.
/// </summary>
/// <remarks>
///     Closing the window while an export is running is guarded by <c>WindowCloseGuardBehavior</c>, driven
///     by the view model's <see cref="VirtualTextureDialogViewModel.CanClose" /> property.
/// </remarks>
public partial class VirtualTextureDialog
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="VirtualTextureDialog" /> class.
    /// </summary>
    public VirtualTextureDialog()
    {
        InitializeComponent();
        // Fix the direction after the XAML is loaded, in case assigning the Flyout to the SplitButton
        // resets it: the menu must always open downwards.
        ExportFlyout.Placement = FlyoutPlacementMode.Bottom;
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
        {
            SearchBox.Text = viewModel.InitialSearch ?? string.Empty;

            // MenuFlyout items live in a separate visual tree, so give them the view model explicitly
            // instead of relying on DataContext inheritance.
            ExportSearchResultsMenuItem.DataContext = viewModel;
            ExportAllMenuItem.DataContext = viewModel;
        }
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
