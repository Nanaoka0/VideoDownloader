using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;

namespace VideoDownloader.Views;

public partial class AboutView : Window
{
    public AboutView()
    {
        InitializeComponent();
        OkButton.Command = ReactiveCommand.Create(Close);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}