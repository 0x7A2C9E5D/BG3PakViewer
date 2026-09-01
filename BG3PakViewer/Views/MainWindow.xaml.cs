using System.Windows;
using BG3PakViewer.Locales;
using BG3PakViewer.Messaging;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using iNKORE.UI.WPF.Modern;
using Serilog;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace BG3PakViewer.Views;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        RegisterMessageHandlers();
    }

    private void RegisterMessageHandlers()
    {
        RegisterFileOpenedMessageHandlers();
        RegisterExportMessageHandlers();
        RegisterClipboardMessageHandler();
    }

    private void RegisterClipboardMessageHandler()
    {
        WeakReferenceMessenger.Default.Register<MainWindow, AsyncRequestMessage<string, bool>, string>(
            this,
            MessageTokens.CopyToClipboard,
            (_, m) => m.Reply(TrySetClipboardText(m.Request)));
    }

    private static bool TrySetClipboardText(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to copy text to clipboard");
            return false;
        }
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
            WeakReferenceMessenger.Default.Register<MainWindow, AsyncRequestMessage<string, bool>, string>(
                this,
                token,
                (_, m) =>
                {
                    m.Reply(MessageBox.Show(message(), caption(), MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                            MessageBoxResult.Yes);
                });

        WeakReferenceMessenger.Default.Register<MainWindow, ValueChangedMessage<string>, string>(
            this,
            MessageTokens.OpenFileFailed,
            (_, _) =>
            {
                MessageBox.Show(Strings.OpenFileFailedMessage, Strings.OpenFileFailedCaption,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });

        WeakReferenceMessenger.Default.Register<ValueChangedMessage<string>, string>(this,
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
            WeakReferenceMessenger.Default.Register<ValueChangedMessage<string>, string>(this, token,
                (_, _) => { MessageBox.Show(message(), caption(), MessageBoxButton.OK, image); });

        WeakReferenceMessenger.Default.Register<AsyncRequestMessage<bool>, string>(this,
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