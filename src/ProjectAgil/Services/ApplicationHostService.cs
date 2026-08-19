using Microsoft.Extensions.DependencyInjection;
using ProjectAgil.Views;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace ProjectAgil.Services;

public sealed class ApplicationHostService(IServiceProvider provider) : IHostedService
{
    private INavigationWindow? _window;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Application.Current.Windows.OfType<MainWindow>().Any())
        {
            return Task.CompletedTask;
        }

        var settings = provider.GetRequiredService<ISettingsService>().Current;

        var theme = settings.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? ApplicationTheme.Light
            : ApplicationTheme.Dark;

        ApplicationThemeManager.Apply(theme);

        try
        {
            var accent = (System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString(settings.AccentColor);

            ApplicationAccentColorManager.Apply(accent, theme);
        }
        catch
        {
        }

        _window = provider.GetRequiredService<INavigationWindow>();
        _window.ShowWindow();
        _ = _window.Navigate(typeof(Views.Pages.DashboardPage));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
