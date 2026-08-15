using System.Threading.Tasks;
using Avalonia.Controls;
using VideoDownloader.ViewModels;
using VideoDownloader.Views;

namespace VideoDownloader.Services;

internal class WindowService : IDialogService
{
    private readonly Window _mainWindow;
    private readonly SettingsViewModel _settingsViewModel;

    public WindowService(Window mainWindow, SettingsViewModel settingsViewModel)
    {
        _mainWindow = mainWindow;
        _settingsViewModel = settingsViewModel;
    }

    public async Task ShowSettingsDialogAsync()
    {
        var window = new SettingsView { DataContext = _settingsViewModel };
        _settingsViewModel.DependencyViewModel.CheckDependenciesCommand.Execute();
        await window.ShowDialog(_mainWindow);
    }

    public async Task ShowAboutDialogAsync()
    {
        var window = new AboutView();
        await window.ShowDialog(_mainWindow);
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var window = new MessageView();
        window.SetContent(title, message);
        await window.ShowDialog(_mainWindow);
    }
}