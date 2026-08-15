using System.Threading.Tasks;

namespace VideoDownloader.Services;

public interface IDialogService
{
    Task ShowSettingsDialogAsync();
    Task ShowAboutDialogAsync();
    Task ShowMessageAsync(string title, string message);
}
