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

public partial class VideoConversionView : UserControl
{
    public VideoConversionView()
    {
        InitializeComponent();

        this.WhenAnyValue(x => x.DataContext)
            .Where(x => x is VideoConversionViewModel)
            .Select(x => (VideoConversionViewModel)x!)
            .Subscribe(vm =>
            {
                vm.PickVideoFiles.RegisterHandler(PickVideoFilesAsync);
                _ = vm.RefreshEncodersAsync();
            });
    }

    private async Task PickVideoFilesAsync(IInteractionContext<Unit, string[]> context)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            context.SetOutput(Array.Empty<string>());
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择要转换的视频文件",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("视频文件")
                {
                    Patterns = new[] { "*.mp4", "*.mkv", "*.webm", "*.mov", "*.avi", "*.flv" }
                }
            }
        });

        context.SetOutput(files.Select(f => f.Path.LocalPath).ToArray());
    }
}