using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ReactiveUI;
using VisualTreeAttachmentEventArgs = Avalonia.VisualTreeAttachmentEventArgs;
using VideoDownloader.ViewModels;

namespace VideoDownloader.Views;

public partial class DownloadView : UserControl
{
    private IDisposable? _handlerSub;

    public DownloadView()
    {
        InitializeComponent();

        this.WhenAnyValue(x => x.DataContext)
            .Where(x => x is DownloadViewModel)
            .Select(x => (DownloadViewModel)x!)
            .Subscribe(vm =>
            {
                _handlerSub?.Dispose();
                _handlerSub = vm.PickOutputFolder.RegisterHandler(PickOutputFolderAsync);
            });
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _handlerSub?.Dispose();
        _handlerSub = null;
        base.OnDetachedFromVisualTree(e);
    }

    private async Task PickOutputFolderAsync(IInteractionContext<Unit, string?> context)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            context.SetOutput(null);
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择导出目录",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        context.SetOutput(folder?.TryGetLocalPath());
    }
}