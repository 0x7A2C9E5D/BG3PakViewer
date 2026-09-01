using System.ComponentModel;
using System.Windows;
using HanumanInstitute.MvvmDialogs;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace BG3PakViewer.Shared.Extensions;

/// <summary>
///     Adds themed message boxes to <see cref="IDialogService" />, so that view models can inform or
///     question the user themselves instead of asking the view to do it.
/// </summary>
/// <remarks>
///     The API deliberately avoids platform enumeration types such as
///     <c>System.Windows.MessageBoxImage</c>: those are mapped here, in the dialog layer, which is
///     the only place that is allowed to know about them.
/// </remarks>
public static class DialogServiceExtensions
{
    extension(IDialogService service)
    {
        /// <summary>Shows a notification that the user acknowledges with a single OK button.</summary>
        public Task NotifyAsync(INotifyPropertyChanged owner,
            string content,
            string title,
            DialogSeverity severity = DialogSeverity.Information)
        {
            return Task.FromResult(Show(service, owner, content, title, MessageBoxButton.OK, severity));
        }

        /// <summary>Asks the user a yes/no question.</summary>
        public Task<bool> ConfirmAsync(INotifyPropertyChanged owner,
            string content,
            string title,
            DialogSeverity severity = DialogSeverity.Information)
        {
            return Task.FromResult(Show(service, owner, content, title, MessageBoxButton.YesNo, severity) ==
                                   MessageBoxResult.Yes);
        }
    }

    private static MessageBoxResult Show(
        IDialogService service,
        INotifyPropertyChanged owner,
        string content,
        string title,
        MessageBoxButton button,
        DialogSeverity severity)
    {
        ArgumentNullException.ThrowIfNull(owner);

        // The owner is resolved through the dialog manager to keep the box on top of the window
        // that owns the calling view model, matching how the other framework dialogs behave.
        var ownerWindow = service.DialogManager.FindViewByViewModel(owner)?.RefObj as Window;
        return ownerWindow == null
            ? MessageBox.Show(content, title, button, MapIcon(severity))
            : MessageBox.Show(ownerWindow, content, title, button, MapIcon(severity));
    }

    private static MessageBoxImage MapIcon(DialogSeverity severity)
    {
        return severity switch
        {
            DialogSeverity.Error => MessageBoxImage.Error,
            DialogSeverity.Warning => MessageBoxImage.Warning,
            _ => MessageBoxImage.Information
        };
    }
}
