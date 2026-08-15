using System;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Avalonia;
using VideoDownloader.ViewModels;

namespace VideoDownloader;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is MainWindowViewModel vm)
                ((ICommand)vm.OpenSettingsCommand).Execute(null);
        }, DispatcherPriority.Background);
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is MainWindowViewModel vm)
                ((ICommand)vm.OpenAboutCommand).Execute(null);
        }, DispatcherPriority.Background);
    }
}