using CommunityToolkit.Mvvm.ComponentModel;
using HanumanInstitute.MvvmDialogs;

namespace BG3PakViewer.Dialogs.ViewModels;

public class AboutDialogViewModel(IReadOnlyDictionary<string, object?> aboutInfo)
    : ObservableObject, IModalDialogViewModel
{
    public IReadOnlyDictionary<string, object?> AboutInfo => aboutInfo;

    public bool? DialogResult => true;
}