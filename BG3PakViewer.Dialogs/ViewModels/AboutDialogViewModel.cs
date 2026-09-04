using CommunityToolkit.Mvvm.ComponentModel;
using HanumanInstitute.MvvmDialogs;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     AboutDialogViewModel
/// </summary>
/// <param name="aboutInfo"></param>
public class AboutDialogViewModel(IReadOnlyDictionary<string, object?> aboutInfo)
    : ObservableObject, IModalDialogViewModel
{
    /// <summary>
    ///     About info.
    /// </summary>
    public IReadOnlyDictionary<string, object?> AboutInfo => aboutInfo;

    /// <summary>
    ///     Dialog result.
    /// </summary>
    public bool? DialogResult => true;
}