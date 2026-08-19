using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using ProjectAgil.Helpers;
using ProjectAgil.Services;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace ProjectAgil;

public partial class App
{
    private static readonly IHost AppHost = Host.CreateDefaultBuilder()
        .ConfigureServices(services =>
        {
            services.AddNavigationViewPageProvider();
            services.AddHostedService<ApplicationHostService>();
            services.AddHostedService<ProfileAutoSaveService>();
            services.AddHostedService<UpdateCheckService>();

            services.AddSingleton<ISnackbarService, SnackbarService>();
            services.AddSingleton<IContentDialogService, ContentDialogService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<ITaskBarService, TaskBarService>();

            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<IRegistryService, RegistryService>();
            services.AddSingleton<IElevationService, ElevationService>();
            services.AddSingleton<IStartupService, StartupService>();
            services.AddSingleton<INetworkService, NetworkService>();
            services.AddSingleton<ITweakCatalog, TweakCatalog>();
            services.AddSingleton<IPlanBuilder, PlanBuilder>();
            services.AddSingleton<IBackupService, BackupService>();
            services.AddSingleton<IProfileService, ProfileService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ITweakEngine, TweakEngine>();
            services.AddSingleton<ILatencyMonitor, LatencyMonitor>();
            services.AddSingleton<IUpdateService, UpdateService>();

            services.AddSingleton<INavigationWindow, Views.MainWindow>();
            services.AddSingleton<ViewModels.MainWindowViewModel>();

            services.AddSingleton<Views.Pages.DashboardPage>();
            services.AddSingleton<ViewModels.DashboardViewModel>();
            services.AddSingleton<Views.Pages.OptimizePage>();
            services.AddSingleton<ViewModels.OptimizeViewModel>();
            services.AddSingleton<Views.Pages.TweaksPage>();
            services.AddSingleton<ViewModels.TweaksViewModel>();
            services.AddSingleton<Views.Pages.MonitorPage>();
            services.AddSingleton<ViewModels.MonitorViewModel>();
            services.AddSingleton<Views.Pages.AdaptersPage>();
            services.AddSingleton<ViewModels.AdaptersViewModel>();
            services.AddSingleton<Views.Pages.ProfilesPage>();
            services.AddSingleton<ViewModels.ProfilesViewModel>();
            services.AddSingleton<Views.Pages.BackupsPage>();
            services.AddSingleton<ViewModels.BackupsViewModel>();
            services.AddSingleton<Views.Pages.ToolsPage>();
            services.AddSingleton<ViewModels.ToolsViewModel>();
            services.AddSingleton<Views.Pages.SettingsPage>();
            services.AddSingleton<ViewModels.SettingsViewModel>();
        })
        .Build();

    private static Mutex? InstanceLock;

    private static bool IsFirstInstance;

    public static IServiceProvider Services => AppHost.Services;

    public static T Resolve<T>()
        where T : notnull => AppHost.Services.GetRequiredService<T>();

    private static bool ClaimSingleInstance()
    {
        InstanceLock = new Mutex(true, "Project-Agil-single-instance", out var first);

        return first;
    }

    private static void SuppressFocusVisuals()
    {
        FrameworkElement.FocusVisualStyleProperty.OverrideMetadata(
            typeof(Control),
            new FrameworkPropertyMetadata(null, null, static (_, _) => null)
        );

        FrameworkElement.FocusVisualStyleProperty.OverrideMetadata(
            typeof(Page),
            new FrameworkPropertyMetadata(null, null, static (_, _) => null)
        );
    }

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        IsFirstInstance = ClaimSingleInstance();

        if (!IsFirstInstance)
        {
            _ = MessageBox.Show(
                "Project-Agil is already open.\n\n"
                    + "Pressing the X only hides the window, it keeps running. "
                    + "Look for the Project-Agil icon next to the clock and click it to get the window back.",
                "Project-Agil is already running",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            Shutdown();

            return;
        }

        SuppressFocusVisuals();
        AppIcon.FollowTheme();
        AppPaths.EnsureCreated();
        await AppHost.StartAsync();
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        if (!IsFirstInstance)
        {
            InstanceLock?.Dispose();

            return;
        }

        try
        {
            Resolve<ILatencyMonitor>().Stop();
            Resolve<ISettingsService>().Save();
            Resolve<IProfileService>().SaveActive();
        }
        catch
        {
        }

        await AppHost.StopAsync();
        AppHost.Dispose();

        InstanceLock?.Dispose();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var path = Path.Combine(AppPaths.Logs, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(path, e.Exception.ToString());
        }
        catch
        {
        }

        _ = MessageBox.Show(
            $"Something went wrong.\n\n{e.Exception.Message}\n\nA log was written to {AppPaths.Logs}.",
            "Project-Agil",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );

        e.Handled = true;
    }
}
