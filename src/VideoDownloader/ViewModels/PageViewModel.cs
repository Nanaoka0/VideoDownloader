using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace VideoDownloader.ViewModels;

public abstract partial class PageViewModel : ReactiveObject
{
    public abstract string Header { get; }

    /// <summary>按钮边框色；null 时使用主题默认。</summary>
    public abstract IBrush? BorderBrush { get; }

    /// <summary>按钮/内容卡片配色；null 时使用主题默认色（本地优先，空则回退）。</summary>
    public abstract IBrush? Fill { get; }

    /// <summary>该页面对应的实际 ViewModel（内容区绑定用）。</summary>
    public abstract object Content { get; }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }
}

public class DownloadPageViewModel : PageViewModel
{
    private readonly DownloadViewModel _vm;

    public DownloadPageViewModel(DownloadViewModel vm)
    {
        _vm = vm;
    }

    public override string Header => "下载";

    public override IBrush? BorderBrush => null;

    public override IBrush? Fill => null;

    public override object Content => _vm;
}

public class ConversionPageViewModel : PageViewModel
{
    private readonly VideoConversionViewModel _vm;

    public ConversionPageViewModel(VideoConversionViewModel vm)
    {
        _vm = vm;
    }

    public override string Header => "转码";

    public override IBrush? BorderBrush => null;

    public override IBrush? Fill => null;

    public override object Content => _vm;
}