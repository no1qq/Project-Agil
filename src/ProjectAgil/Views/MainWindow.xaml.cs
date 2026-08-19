using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ProjectAgil.Helpers;
using ProjectAgil.Services;
using ProjectAgil.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ProjectAgil.Views;

public partial class MainWindow : INavigationWindow
{
    private const double OccludedBelow = 0.5d;
    private const double ClearAbove = 0.58d;

    private readonly ISettingsService _settings;
    private readonly ILatencyMonitor _monitor;
    private readonly DispatcherTimer _visibilityTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };

    private nint _handle;
    private bool _occluded = true;
    private bool _exiting;

    public MainWindow(
        MainWindowViewModel viewModel,
        INavigationService navigationService,
        ISnackbarService snackbarService,
        IContentDialogService contentDialogService,
        INavigationViewPageProvider pageProvider,
        ISettingsService settings,
        ILatencyMonitor monitor
    )
    {
        ViewModel = viewModel;
        _settings = settings;
        _monitor = monitor;
        DataContext = this;

        SystemThemeWatcher.Watch(this);

        InitializeComponent();

        RootNavigation.SetPageProviderService(pageProvider);
        navigationService.SetNavigationControl(RootNavigation);
        snackbarService.SetSnackbarPresenter(RootSnackbar);
        contentDialogService.SetDialogHost(RootContentDialog);

        viewModel.AttachWindow(this);

        _monitor.Pause();

        _visibilityTimer.Tick += OnVisibilityTick;
        Loaded += OnLoaded;
    }

    public MainWindowViewModel ViewModel { get; }

    public INavigationView GetNavigation() => RootNavigation;

    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

    public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
        RootNavigation.SetPageProviderService(navigationViewPageProvider);

    public void ShowWindow() => Show();

    public void CloseWindow() => Close();

    public void SetServiceProvider(IServiceProvider serviceProvider) { }

    public void ExitApplication()
    {
        _exiting = true;
        Close();
    }

    public void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        _ = Activate();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        UpdateLiveStats();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        UpdateLiveStats();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting && _settings.Current.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            UpdateLiveStats();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        if (WindowState == WindowState.Minimized && _settings.Current.MinimizeToTray)
        {
            Hide();
        }

        UpdateLiveStats();
    }

    protected override void OnClosed(EventArgs e)
    {
        _visibilityTimer.Stop();
        _visibilityTimer.Tick -= OnVisibilityTick;

        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        PrepareContentHost();

        _handle = new WindowInteropHelper(this).Handle;
        UpdateLiveStats();
        _visibilityTimer.Start();
    }

    private void OnVisibilityTick(object? sender, EventArgs e) => UpdateLiveStats();

    private void UpdateLiveStats()
    {
        if (!IsVisible || WindowState == WindowState.Minimized)
        {
            _occluded = true;
            _monitor.Pause();
            return;
        }

        if (_handle == 0)
        {
            return;
        }

        var visible = WindowOcclusion.VisibleFraction(_handle);
        _occluded = _occluded ? visible < ClearAbove : visible < OccludedBelow;

        if (_occluded)
        {
            _monitor.Pause();
        }
        else
        {
            _monitor.Resume();
        }
    }

    private void PrepareContentHost()
    {
        _ = RootNavigation.ApplyTemplate();

        var host = FindDescendant<NavigationViewContentPresenter>(RootNavigation);
        if (host is null)
        {
            return;
        }

        host.Transition = Wpf.Ui.Animations.Transition.None;
        host.TransitionDuration = 0;

        _ = host.ApplyTemplate();

        var scroller = FindDescendant<DynamicScrollViewer>(host);
        if (scroller is null)
        {
            return;
        }

        scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is T match)
            {
                return match;
            }

            var deeper = FindDescendant<T>(child);
            if (deeper is not null)
            {
                return deeper;
            }
        }

        return null;
    }
}
