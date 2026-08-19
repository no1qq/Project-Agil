using System.Windows.Controls;
using ProjectAgil.Views;
using Wpf.Ui;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace ProjectAgil.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    private MainWindow? _window;

    [ObservableProperty]
    private string _applicationTitle = "Project-Agil";

    [ObservableProperty]
    private ObservableCollection<object> _navigationItems = [];

    [ObservableProperty]
    private ObservableCollection<object> _navigationFooter = [];

    [ObservableProperty]
    private ObservableCollection<MenuItem> _trayMenuItems = [];

    public MainWindowViewModel(INavigationService navigation)
    {
        _navigation = navigation;

        NavigationItems =
        [
            new NavigationViewItem
            {
                Content = "Dashboard",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(Views.Pages.DashboardPage),
            },
            new NavigationViewItem
            {
                Content = "Optimize",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Flash24 },
                TargetPageType = typeof(Views.Pages.OptimizePage),
            },
            new NavigationViewItem
            {
                Content = "All settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Options24 },
                TargetPageType = typeof(Views.Pages.TweaksPage),
            },
            new NavigationViewItem
            {
                Content = "Watch my ping",
                Icon = new SymbolIcon { Symbol = SymbolRegular.PulseSquare24 },
                TargetPageType = typeof(Views.Pages.MonitorPage),
            },
            new NavigationViewItem
            {
                Content = "Network cards",
                Icon = new SymbolIcon { Symbol = SymbolRegular.PlugConnected24 },
                TargetPageType = typeof(Views.Pages.AdaptersPage),
            },
            new NavigationViewItem
            {
                Content = "Saved setups",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Bookmark24 },
                TargetPageType = typeof(Views.Pages.ProfilesPage),
            },
            new NavigationViewItem
            {
                Content = "Undo points",
                Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowUndo24 },
                TargetPageType = typeof(Views.Pages.BackupsPage),
            },
            new NavigationViewItem
            {
                Content = "Fix and check",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Wrench24 },
                TargetPageType = typeof(Views.Pages.ToolsPage),
            },
        ];

        NavigationFooter =
        [
            new NavigationViewItem
            {
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage),
            },
        ];

        TrayMenuItems =
        [
            new MenuItem { Header = "Open Project-Agil", Command = ShowWindowCommand },
            new MenuItem { Header = "Optimize my connection", Command = OpenOptimizeCommand },
            new MenuItem { Header = "Watch my ping", Command = OpenMonitorCommand },
            new MenuItem { Header = "Exit", Command = ExitCommand },
        ];
    }

    public void AttachWindow(MainWindow window) => _window = window;

    [RelayCommand]
    private void ShowWindow() => _window?.RestoreFromTray();

    [RelayCommand]
    private void OpenOptimize()
    {
        _window?.RestoreFromTray();
        _ = _navigation.Navigate(typeof(Views.Pages.OptimizePage));
    }

    [RelayCommand]
    private void OpenMonitor()
    {
        _window?.RestoreFromTray();
        _ = _navigation.Navigate(typeof(Views.Pages.MonitorPage));
    }

    [RelayCommand]
    private void Exit() => _window?.ExitApplication();
}
