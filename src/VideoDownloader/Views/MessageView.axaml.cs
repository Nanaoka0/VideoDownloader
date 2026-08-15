using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;

namespace VideoDownloader.Views;

public partial class MessageView : Window
{
    public MessageView()
    {
        InitializeComponent();
        OkButton.Command = ReactiveCommand.Create(Close);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    public void SetContent(string title, string message)
    {
        Title = title;
        TitleBarText.Text = title;
        MessageText.Text = message;
    }
}