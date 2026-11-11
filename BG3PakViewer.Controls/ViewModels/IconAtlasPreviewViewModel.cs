using System.Collections.ObjectModel;
using System.Windows.Input;
using BG3PakViewer.Controls.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Syncfusion.Data.Extensions;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing a sliced icon atlas as a grid of icons.
/// </summary>
/// <remarks>
///     The atlas is sliced by the loader, so this view model only holds the icons it is given; the size
///     of the cells comes from the sliced bitmaps themselves.
///     The icons are one flat list; how many of them share a line is decided by the wrap panel of the
///     view, which puts as many on a line as its width allows.
/// </remarks>
public partial class IconAtlasPreviewViewModel : ObservableObject, IPreviewKeyHandler
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="IconAtlasPreviewViewModel" /> class.
    /// </summary>
    /// <param name="items">The icons of the atlas, with the rectangles they were cut from.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public IconAtlasPreviewViewModel(IEnumerable<IconAtlasCellViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items.ToObservableCollection();
    }

    /// <summary>
    ///     The icons of the atlas that hold something visible, in the order of the atlas.
    /// </summary>
    public ObservableCollection<IconAtlasCellViewModel> Items { get; }

    /// <summary>
    ///     Maps the shortcut keys of the preview to the selection commands.
    /// </summary>
    /// <param name="e">The key event forwarded by the previewer.</param>
    /// <returns>Whether the key was handled.</returns>
    public bool HandleKeyDown(KeyEventArgs e)
    {
        ICommand? command = (e.Key, Keyboard.Modifiers) switch
        {
            (Key.A, ModifierKeys.Control) => SelectAllCommand,
            (Key.I, ModifierKeys.Control | ModifierKeys.Shift) => InvertSelectionCommand,
            (Key.D, ModifierKeys.Control) => ClearSelectionCommand,
            _ => null
        };
        if (command is null) return false;

        command.Execute(null);
        return true;
    }

    /// <summary>
    ///     Selects every icon of the atlas.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in Items)
            item.IsSelected = true;
    }

    /// <summary>
    ///     Inverts the selection of every icon of the atlas.
    /// </summary>
    [RelayCommand]
    private void InvertSelection()
    {
        foreach (var item in Items)
            item.IsSelected = !item.IsSelected;
    }

    /// <summary>
    ///     Clears the selection of every icon of the atlas.
    /// </summary>
    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var item in Items)
            item.IsSelected = false;
    }
}