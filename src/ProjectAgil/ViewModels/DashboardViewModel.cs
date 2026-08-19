using ProjectAgil.Models;
using ProjectAgil.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ProjectAgil.ViewModels;

public partial class DashboardViewModel(
    INetworkService network,
    ISettingsService settings,
    IProfileService profiles,
    IPlanBuilder planBuilder,
    ITweakEngine engine,
    IBackupService backups,
    IElevationService elevation,
    ILatencyMonitor monitor,
    INavigationService navigation,
    ISnackbarService snackbar,
    IUpdateService updates
) : PageViewModel
{
    private bool _hooked;

    [ObservableProperty]
    private bool _isElevated = true;

    [ObservableProperty]
    private bool _showElevationWarning;

    [ObservableProperty]
    private string _adapterName = "No adapter";

    [ObservableProperty]
    private string _adapterDetail = string.Empty;

    [ObservableProperty]
    private string _profileSummary = string.Empty;

    [ObservableProperty]
    private int _optimizedCount;

    [ObservableProperty]
    private int _planCount;

    [ObservableProperty]
    private double _optimizationPercent;

    [ObservableProperty]
    private string _optimizationState = "Not measured";

    [ObservableProperty]
    private bool _hasRestorePoint;

    [ObservableProperty]
    private int _restorePointCount;

    [ObservableProperty]
    private LatencyStats? _primary;

    [ObservableProperty]
    private IReadOnlyList<double> _history = [];

    [ObservableProperty]
    private string _monitorTarget = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _activity = [];

    [ObservableProperty]
    private bool _updateAvailable;

    [ObservableProperty]
    private bool _updateBusy;

    [ObservableProperty]
    private string _updateState = string.Empty;

    public override async Task OnNavigatedToAsync()
    {
        IsElevated = elevation.IsElevated;
        ShowElevationWarning = !IsElevated;

        if (!_hooked)
        {
            monitor.Updated += OnMonitorUpdated;
            updates.AvailableChanged += OnUpdateAvailableChanged;
            _hooked = true;
        }

        UpdateAvailable = updates.Available is not null;

        EnsureMonitor();
        await RefreshAsync().ConfigureAwait(false);
    }

    private void EnsureMonitor()
    {
        var config = settings.Current;
        var target = config.Targets.FirstOrDefault(t => t.IsPrimary && t.Enabled) ?? config.Targets.FirstOrDefault();

        MonitorTarget = target is null ? "no target" : $"{target.Name}  ({target.Host})";

        if (!monitor.IsRunning && config.Targets.Any(t => t.Enabled))
        {
            monitor.Start(config.Targets, config.PingIntervalMs, config.PingTimeoutMs, config.HistorySize);
        }
    }

    private void OnUpdateAvailableChanged(object? sender, EventArgs e) =>
        OnUi(() => UpdateAvailable = updates.Available is not null);

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        var info = updates.Available;

        if (info is null || UpdateBusy)
        {
            return;
        }

        UpdateBusy = true;
        UpdateState = "Downloading";

        try
        {
            var progress = new Progress<double>(p => OnUi(() => UpdateState = $"Downloading {p:0}%"));

            await updates.InstallAsync(info, progress).ConfigureAwait(false);

            OnUi(() =>
            {
                UpdateState = "Restarting";
                Application.Current.Shutdown();
            });
        }
        catch (Exception ex)
        {
            OnUi(() =>
            {
                UpdateBusy = false;
                UpdateState = string.Empty;

                snackbar.Show(
                    "Update failed",
                    ex.Message,
                    ControlAppearance.Danger,
                    null,
                    TimeSpan.FromSeconds(6)
                );
            });
        }
    }

    private void OnMonitorUpdated(object? sender, IReadOnlyList<LatencyStats> stats)
    {
        var config = settings.Current;
        var target = config.Targets.FirstOrDefault(t => t.IsPrimary && t.Enabled) ?? config.Targets.FirstOrDefault();

        if (target is null)
        {
            return;
        }

        var match = stats.FirstOrDefault(s => s.Host == target.Host) ?? stats.FirstOrDefault();
        var history = monitor.History(target.Host);

        OnUi(() =>
        {
            Primary = match;
            History = history;
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        BusyMessage = "Reading current network state";

        try
        {
            var adapter = network.GetPreferredAdapter(settings.Current.PreferredAdapterId);

            OnUi(() =>
            {
                AdapterName = adapter?.Name ?? "No active adapter";
                AdapterDetail = adapter is null
                    ? "Connect a network adapter and refresh"
                    : $"{adapter.Description}  -  {adapter.SpeedDisplay}  -  {adapter.IPv4}";
                ProfileSummary = profiles.Active.Summary;
            });

            var context = await engine.CreateContextAsync(adapter).ConfigureAwait(false);
            var plan = planBuilder.Build(profiles.Active, context);

            var optimized = plan.Count(p => p.Status == TweakStatus.Optimized);
            var snapshots = backups.LoadAll();
            var active = snapshots.Where(s => !s.Restored).ToList();

            OnUi(() =>
            {
                PlanCount = plan.Count;
                OptimizedCount = optimized;
                OptimizationPercent = plan.Count == 0 ? 0 : Math.Round(optimized * 100d / plan.Count);
                OptimizationState = plan.Count == 0
                    ? "Nothing planned"
                    : optimized == plan.Count
                        ? "Fully optimized"
                        : optimized == 0
                            ? "Untouched"
                            : "Partially optimized";

                RestorePointCount = active.Count;
                HasRestorePoint = active.Count > 0;

                Activity.Clear();

                foreach (var snapshot in snapshots.Take(6))
                {
                    Activity.Add(
                        $"{snapshot.CreatedDisplay}   {snapshot.EntryCount} changes   {snapshot.ProfileName}   {snapshot.StatusDisplay}"
                    );
                }

                if (Activity.Count == 0)
                {
                    Activity.Add("No changes have been made yet.");
                }
            });
        }
        catch (Exception ex)
        {
            OnUi(() => snackbar.Show("Refresh failed", ex.Message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(6)));
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    [RelayCommand]
    private void OpenOptimize() => _ = navigation.Navigate(typeof(Views.Pages.OptimizePage));

    [RelayCommand]
    private void OpenMonitor() => _ = navigation.Navigate(typeof(Views.Pages.MonitorPage));

    [RelayCommand]
    private void OpenBackups() => _ = navigation.Navigate(typeof(Views.Pages.BackupsPage));

    [RelayCommand]
    private async Task RevertAllAsync()
    {
        IsBusy = true;
        BusyMessage = "Reverting every change";

        try
        {
            var result = await engine.RestoreAllAsync().ConfigureAwait(false);

            OnUi(() =>
                snackbar.Show(
                    result.Failed == 0 ? "Reverted"
                        : result.Restored == 0 ? "Nothing could be put back"
                        : "Partly put back",
                    result.Failed == 0
                        ? $"{result.Restored} settings were put back to their previous values."
                        : $"{result.Restored} settings were put back, {result.Failed} could not be.",
                    result.Failed == 0 ? ControlAppearance.Success
                        : result.Restored == 0 ? ControlAppearance.Danger
                        : ControlAppearance.Caution,
                    null,
                    TimeSpan.FromSeconds(result.Failed == 0 ? 5 : 8)
                )
            );

            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OnUi(() => snackbar.Show("Revert failed", ex.Message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(6)));
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    [RelayCommand]
    private void Elevate() => elevation.RestartElevated();
}
