using Wpf.Ui.Abstractions.Controls;

namespace ProjectAgil.ViewModels;

public abstract partial class PageViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyMessage = string.Empty;

    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;

    public virtual Task OnNavigatedFromAsync() => Task.CompletedTask;

    protected static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = dispatcher.BeginInvoke(action);
    }
}
