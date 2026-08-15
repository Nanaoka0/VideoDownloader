using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;
using VideoDownloader.ViewModels;

namespace VideoDownloader.Views;

public partial class DownloadView : UserControl
{
    public DownloadView()
    {
        InitializeComponent();

        this.WhenAnyValue(x => x.DataContext)
            .Where(x => x is DownloadViewModel)
            .Select(x => (DownloadViewModel)x!)
            .Subscribe(vm => vm.PickOutputFolder.RegisterHandler(PickOutputFolderAsync));
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

        context.SetOutput(folders.FirstOrDefault()?.Path.LocalPath);
    }
}