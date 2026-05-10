using System.Windows;
using BG3PakViewer.Locales;
using BG3PakViewer.Messaging;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using iNKORE.UI.WPF.Modern;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace BG3PakViewer.Views;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private readonly IMessenger _messenger;

    public MainWindow()
    {
        InitializeComponent();
        RegisterMessageHandlers(out _messenger);
    }

    private void RegisterMessageHandlers(out IMessenger messenger)
    {
        messenger = Ioc.Default.GetRequiredService<IMessenger>();
        RegisterFileOpenedMessageHandlers();
        RegisterExportMessageHandlers();
    }

    private void RegisterFileOpenedMessageHandlers()
    {
        var messageHandlers = new (string, Func<string>, Func<string>)[]
        {
            (MessageTokens.ReOpenFile, () => Strings.ReOpenFileMessage, () => Strings.ReOpenFileCaption),
            (MessageTokens.OpenedFileNoFound, () => Strings.FileOpenedNoFoundMessage,
                () => Strings.FileOpenedNoFoundCaption)
        };
        foreach (var (token, message, caption) in messageHandlers)
            _messenger.Register<MainWindow, AsyncRequestMessage<string, bool>, string>(
                this,
                token,
                (_, m) =>
                {
                    m.Reply(MessageBox.Show(message(), caption(), MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                            MessageBoxResult.Yes);
                });

        _messenger.Register<MainWindow, ValueChangedMessage<string>, string>(
            this,
            MessageTokens.OpenFileFailed,
            (_, _) =>
            {
                MessageBox.Show(Strings.OpenFileFailedMessage, Strings.OpenFileFailedCaption,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });

        _messenger.Register<ValueChangedMessage<string>, string>(this,
            MessageTokens.FileLoadingDuplicate,
            (_, _) =>
            {
                MessageBox.Show(Strings.FileLoadingDuplicateMessage, Strings.FileLoadingDuplicateCaption,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            });
    }

    private void RegisterExportMessageHandlers()
    {
        var valueChangedHandlers =
            new (string token, Func<string> message, Func<string> caption, MessageBoxImage image)[]
            {
                (MessageTokens.ExportCompleted, () => Strings.ExportCompleted, () => Strings.ExportCompleted,
                    MessageBoxImage.Information),
                (MessageTokens.ExportFailed, () => Strings.ExportFailedMessage, () => Strings.ExportFailedCaption,
                    MessageBoxImage.Error)
            };

        foreach (var (token, message, caption, image) in valueChangedHandlers)
            _messenger.Register<ValueChangedMessage<string>, string>(this, token,
                (_, _) => { MessageBox.Show(message(), caption(), MessageBoxButton.OK, image); });

        _messenger.Register<AsyncRequestMessage<bool>, string>(this,
            MessageTokens.CancelExport,
            (_, m) =>
            {
                m.Reply(MessageBox.Show(Strings.CancelExportOperationMessage, Strings.CancelExportOperationCaption,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes);
            });
    }

    private void ThemeSwitchBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.Current.ApplicationTheme =
            ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Light
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
    }
}