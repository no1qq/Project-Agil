using System.Windows.Controls;
using ProjectAgil.Services;
using ProjectAgil.Views;
using Wpf.Ui;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace ProjectAgil.ViewModels;

public sealed record NavDefinition(string Label, SymbolRegular Icon, Type PageType, bool Advanced);

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly NavDefinition[] Definitions =
    [
        new("Dashboard", SymbolRegular.Home24, typeof(Views.Pages.DashboardPage), false),
        new("Optimize", SymbolRegular.Flash24, typeof(Views.Pages.OptimizePage), false),
        new("All settings", SymbolRegular.Options24, typeof(Views.Pages.TweaksPage), true),
        new("Watch my ping", SymbolRegular.PulseSquare24, typeof(Views.Pages.MonitorPage), false),
        new("Network cards", SymbolRegular.PlugConnected24, typeof(Views.Pages.AdaptersPage), true),
        new("Saved setups", SymbolRegular.Bookmark24, typeof(Views.Pages.ProfilesPage), false),
        new("Undo points", SymbolRegular.ArrowUndo24, typeof(Views.Pages.BackupsPage), false),
        new("Fix and check", SymbolRegular.Wrench24, typeof(Views.Pages.ToolsPage), false),
    ];

    private readonly INavigationService _navigation;
    private readonly ISettingsService _settings;

    private MainWindow? _window;

    [ObservableProperty]
    private string _applicationTitle = "Project-Agil";

    [ObservableProperty]
    private ObservableCollection<object> _navigationItems = [];

    [ObservableProperty]
    private ObservableCollection<object> _navigationFooter = [];

    [ObservableProperty]
    private ObservableCollection<MenuItem> _trayMenuItems = [];

    public MainWindowViewModel(INavigationService navigation, ISettingsService settings)
    {
        _navigation = navigation;
        _settings = settings;

        RebuildNavigationItems();
        _settings.AdvancedModeChanged += OnAdvancedModeChanged;

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

    public static IReadOnlyList<NavDefinition> VisibleNavigation(bool advanced) =>
        [.. Definitions.Where(d => advanced || !d.Advanced)];

    private void OnAdvancedModeChanged(object? sender, EventArgs e)
    {
        RebuildNavigationItems();

        _window?.GetNavigation().ClearJournal();
    }

    private void RebuildNavigationItems() =>
        NavigationItems =
        [
            .. VisibleNavigation(_settings.Current.AdvancedMode)
                .Select(d => new NavigationViewItem
                {
                    Content = d.Label,
                    Icon = new SymbolIcon { Symbol = d.Icon },
                    TargetPageType = d.PageType,
                }),
        ];

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
