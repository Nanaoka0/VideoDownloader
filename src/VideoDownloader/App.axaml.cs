using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using VideoDownloader.Application.Messenger;
using VideoDownloader.Application.Services.Conversion;
using VideoDownloader.Application.Services.Dependency;
using VideoDownloader.Application.Services.Download;
using VideoDownloader.Application.Services.Download.Sites;
using VideoDownloader.Application.Services.UI;
using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Messenger;
using VideoDownloader.Domain.Services;
using VideoDownloader.Domain.Services.Conversion;
using VideoDownloader.Domain.Services.Dependency;
using VideoDownloader.Domain.Services.Download;
using VideoDownloader.Domain.Services.FileSystem;
using VideoDownloader.Domain.Services.Network;
using VideoDownloader.Domain.Services.Process;
using VideoDownloader.Infrastructure.Logging;
using VideoDownloader.Infrastructure.Services.Dependency;
using VideoDownloader.Infrastructure.Services.Download;
using VideoDownloader.Infrastructure.Services.FileSystem;
using VideoDownloader.Infrastructure.Services.Gpu;
using VideoDownloader.Infrastructure.Services.Network;
using VideoDownloader.Infrastructure.Services.Process;
using VideoDownloader.Services;
using VideoDownloader.ViewModels;
using VideoDownloader.Views;

namespace VideoDownloader;

public partial class App : Avalonia.Application
{
    private static bool _firstRunCheckStarted;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public static void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "app.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogUnhandled("Unhandled", "AppDomain 未处理异常", e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogUnhandled("Unhandled", "Task 未观察异常", e.Exception);
            e.SetObserved();
        };
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            LogUnhandled("Unhandled", "Dispatcher 未处理异常", e.Exception);
            e.Handled = true;
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = BuildServiceProvider();

        var config = services.GetRequiredService<IDownloadConfiguration>();
        ThemeManager.Apply(config.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            var windowService = new WindowService(mainWindow, services.GetRequiredService<SettingsViewModel>());

            var vm = ActivatorUtilities.CreateInstance<MainWindowViewModel>(services, windowService);
            mainWindow.DataContext = vm;

            mainWindow.Loaded += async (_, _) =>
            {
                if (_firstRunCheckStarted) return;
                _firstRunCheckStarted = true;

                if (!config.SuppressFirstRunTips)
                {
                    var firstRun = new FirstRunView();
                    await firstRun.ShowDialog(mainWindow);
                    if (firstRun.SuppressNextRun)
                    {
                        config.SuppressFirstRunTips = true;
                        config.Save();
                    }
                }
            };

            _ = AutoCheckDependenciesAsync(
                services.GetRequiredService<IDependencyService>(),
                services.GetRequiredService<VideoDownloader.Domain.Logging.ILogger>(),
                services.GetRequiredService<IEventMessenger>());
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        return services.BuildServiceProvider();
    }

    private static async Task AutoCheckDependenciesAsync(
        IDependencyService dependencyService,
        VideoDownloader.Domain.Logging.ILogger logger,
        IEventMessenger messenger)
    {
        try
        {
            await dependencyService.CheckDependenciesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogException("Startup", "启动时自动检测依赖失败", ex);
            messenger.Send(new UiPromptMessage("依赖检测失败", $"启动时自动检测依赖失败：{ex.Message}"));
        }
    }

    private static void LogUnhandled(string module, string context, object? exceptionObject)
    {
        try
        {
            var logger = new SerilogLogger();
            if (exceptionObject is Exception ex)
                logger.LogException(module, context, ex);
            else
                logger.LogError(module, $"{context}: {exceptionObject?.ToString()}");
        }
        catch
        {
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<VideoDownloader.Domain.Logging.ILogger, SerilogLogger>();
        services.AddSingleton<IEventMessenger, EventMessenger>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IToolPathResolver, ToolLocationProvider>();
        services.AddSingleton<IGpuInfoService, GpuInfoService>();
        services.AddSingleton<IDownloadConfiguration>(sp =>
            DownloadConfiguration.Load(sp.GetRequiredService<VideoDownloader.Domain.Logging.ILogger>()));

        services.AddSingleton<IUrlAnalysisService, UrlAnalysisService>();
        services.AddSingleton<IFileCleanupService, FileCleanupService>();
        services.AddSingleton<INetworkTestService, NetworkTestService>();

        services.AddSingleton<ISiteDownloader, YoutubeDownloader>();
        services.AddSingleton<ISiteDownloader, BilibiliDownloader>();
        services.AddSingleton<ISiteDownloader, DefaultDownloader>();

        services.AddSingleton<IDependencyService, DependencyService>();
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<IVideoConversionService, VideoConversionService>();
        services.AddSingleton<IVideoEncoderCatalog, VideoEncoderCatalog>();

        services.AddTransient<SettingsViewModel>();
        services.AddTransient<DependencyViewModel>();
        services.AddTransient<DownloadViewModel>();
        services.AddTransient<VideoConversionViewModel>();
        services.AddTransient<MainWindowViewModel>();
    }
}