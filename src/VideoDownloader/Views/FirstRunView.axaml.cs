using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;

namespace VideoDownloader.Views;

public partial class FirstRunView : Window
{
    public bool SuppressNextRun { get; private set; }

    public FirstRunView()
    {
        InitializeComponent();
        OkButton.IsEnabled = false;
        AckCheckBox.IsCheckedChanged += (_, e) =>
        {
            OkButton.IsEnabled = AckCheckBox.IsChecked == true;
        };
        OkButton.Command = ReactiveCommand.Create(() =>
        {
            SuppressNextRun = SuppressCheckBox.IsChecked == true;
            Close();
        });
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}