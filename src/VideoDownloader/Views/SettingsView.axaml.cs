using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;
using ReactiveUI.Avalonia;
using VideoDownloader.ViewModels;

namespace VideoDownloader.Views;

public partial class SettingsView : ReactiveWindow<SettingsViewModel>
{
    public SettingsView()
    {
        InitializeComponent();
        this.WhenActivated(d =>
        {
            if (ViewModel is not null)
                d(ViewModel.CloseWindow.RegisterHandler(_ =>
                {
                    Close();
                    return Task.CompletedTask;
                }));
        });
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}