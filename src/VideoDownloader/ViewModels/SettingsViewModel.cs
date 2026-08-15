using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using VideoDownloader.Domain.Messenger;
using VideoDownloader.Domain.Services;
using VideoDownloader.Domain.Services.Network;
using VideoDownloader.Services;

namespace VideoDownloader.ViewModels;

public partial class SettingsViewModel : ReactiveObject
{
    private readonly IDownloadConfiguration _config;
    private readonly INetworkTestService _networkTestService;
    private readonly IEventMessenger _messenger;

    public DependencyViewModel DependencyViewModel { get; }

    public Interaction<Unit, Unit> CloseWindow { get; } = new();

    [Reactive]
    private bool _useProxy;

    [Reactive]
    private string _proxyMode = "HTTP";

    [Reactive]
    private string _httpProxyHost = "127.0.0.1";

    [Reactive]
    private int _httpProxyPort = 7890;

    [Reactive]
    private string _socks5ProxyHost = "127.0.0.1";

    [Reactive]
    private int _socks5ProxyPort = 7891;

    [Reactive]
    private string _networkTestResult = string.Empty;

    [Reactive]
    private bool _bypassBilibili = true;

    [Reactive]
    private bool _bypassYoutube;

    [Reactive]
    private string _theme = "Default";

    [Reactive]
    private string _cookieBrowser = "None";

    public IReadOnlyList<string> Themes { get; } = ThemeManager.Themes;

    public IReadOnlyList<string> CookieBrowsers { get; } = new[] { "None", "chrome", "edge", "firefox", "brave", "opera", "vivaldi" };

    private readonly ObservableAsPropertyHelper<bool> _isHttpProxy;
    private readonly ObservableAsPropertyHelper<bool> _isSocks5Proxy;

    public bool IsHttpProxy => _isHttpProxy.Value;
    public bool IsSocks5Proxy => _isSocks5Proxy.Value;

    public SettingsViewModel(
        IDownloadConfiguration config,
        INetworkTestService networkTestService,
        DependencyViewModel dependencyViewModel,
        IEventMessenger messenger)
    {
        _config = config;
        _networkTestService = networkTestService;
        DependencyViewModel = dependencyViewModel;
        _messenger = messenger;
        UseProxy = _config.UseProxy;
        ProxyMode = _config.ProxyType;
        HttpProxyHost = _config.ProxyHttpHost;
        HttpProxyPort = _config.ProxyHttpPort;
        Socks5ProxyHost = _config.ProxySocks5Host;
        Socks5ProxyPort = _config.ProxySocks5Port;
        BypassBilibili = _config.BypassProxyForBilibili;
        BypassYoutube = _config.BypassProxyForYoutube;
        Theme = _config.Theme;
        CookieBrowser = _config.CookieBrowser;

        _isHttpProxy = this.WhenAnyValue(x => x.ProxyMode)
            .Select(m => m == "HTTP")
            .ToProperty(this, x => x.IsHttpProxy);

        _isSocks5Proxy = this.WhenAnyValue(x => x.ProxyMode)
            .Select(m => m == "SOCKS5")
            .ToProperty(this, x => x.IsSocks5Proxy);

        this.WhenAnyValue(x => x.Theme)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Subscribe(x => ThemeManager.Apply(x));
    }

    public void Save()
    {
        _config.UseProxy = UseProxy;
        _config.ProxyType = ProxyMode;
        _config.ProxyHttpHost = HttpProxyHost;
        _config.ProxyHttpPort = HttpProxyPort;
        _config.ProxySocks5Host = Socks5ProxyHost;
        _config.ProxySocks5Port = Socks5ProxyPort;
        _config.BypassProxyForBilibili = BypassBilibili;
        _config.BypassProxyForYoutube = BypassYoutube;
        _config.Theme = Theme;
        _config.CookieBrowser = CookieBrowser;
        _config.Save();
    }

    [ReactiveCommand]
    public void SelectHttpProxy() => ProxyMode = "HTTP";

    [ReactiveCommand]
    public void SelectSocks5Proxy() => ProxyMode = "SOCKS5";

    [ReactiveCommand]
    public void SaveAndClose()
    {
        Save();
        CloseWindow.Handle(Unit.Default).Subscribe();
    }

    [ReactiveCommand]
    public void CancelClose() => CloseWindow.Handle(Unit.Default).Subscribe();

    [ReactiveCommand]
    public async Task TestNetwork()
    {
        NetworkTestResult = "正在测试...";
        var host = ProxyMode == "SOCKS5" ? Socks5ProxyHost : HttpProxyHost;
        var port = ProxyMode == "SOCKS5" ? Socks5ProxyPort : HttpProxyPort;
        NetworkTestResult = await _networkTestService.TestNetworkAsync(UseProxy, ProxyMode, host, port);
        _messenger.Send(new UiPromptMessage("网络测试", NetworkTestResult));
    }
}