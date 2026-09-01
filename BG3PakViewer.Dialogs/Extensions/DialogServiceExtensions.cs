using System.ComponentModel;
using System.Windows;
using HanumanInstitute.MvvmDialogs;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace BG3PakViewer.Dialogs.Extensions;

/// <summary>
///     Adds message boxes to <see cref="IDialogService" /> using the iNKORE theme, so that view
///     models can inform or question the user themselves instead of asking the view to do it.
/// </summary>
public static class DialogServiceExtensions
{
    extension(IDialogService? service)
    {
        /// <summary>
        ///     Shows a message box rendered by the current iNKORE theme.
        /// </summary>
        /// <returns>True for OK/Yes, false for No, and null when the dialog was canceled.</returns>
        // ReSharper disable once MemberCanBePrivate.Global
        public Task<bool?> ShowThemedMessageBoxAsync(INotifyPropertyChanged owner,
            string content,
            string title,
            MessageBoxButton button,
            MessageBoxImage icon)
        {
            ArgumentNullException.ThrowIfNull(owner);

            // The owner is resolved through the dialog manager to keep the box on top of the window
            // that owns the calling view model, matching how the other framework dialogs behave.
            var result = service?.DialogManager.FindViewByViewModel(owner)?.RefObj is not Window ownerWindow
                ? MessageBox.Show(content, title, button, icon)
                : MessageBox.Show(ownerWindow, content, title, button, icon);

            return Task.FromResult<bool?>(result switch
            {
                MessageBoxResult.OK => true,
                MessageBoxResult.Yes => true,
                MessageBoxResult.No => false,
                _ => null
            });
        }

        /// <summary>Shows a notification that the user acknowledges with a single OK button.</summary>
        public Task NotifyAsync(INotifyPropertyChanged owner,
            string content,
            string title,
            MessageBoxImage icon = MessageBoxImage.Information)
        {
            return service.ShowThemedMessageBoxAsync(owner, content, title, MessageBoxButton.OK, icon);
        }

        /// <summary>Asks the user a yes/no question.</summary>
        public async Task<bool> ConfirmAsync(INotifyPropertyChanged owner,
            string content,
            string title,
            MessageBoxImage icon = MessageBoxImage.Question)
        {
            return await service.ShowThemedMessageBoxAsync(owner, content, title, MessageBoxButton.YesNo, icon) == true;
        }
    }
}
