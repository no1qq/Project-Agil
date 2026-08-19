using ProjectAgil.Helpers;
using ProjectAgil.Models;
using ProjectAgil.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ProjectAgil.ViewModels;

public sealed record NamedOption<T>(string Label, T Value);

internal enum RunPhase
{
    BeforeMeasurement,
    Applying,
    AfterMeasurement,
}

public sealed class PlanGroup(string name, IEnumerable<PlanItem> items)
{
    public string Name { get; } = name;

    public IReadOnlyList<PlanItem> Items { get; } = [.. items];

    public int Count => Items.Count;

    public string Header => $"{Name}  ({Count})";
}

public partial class OptimizeViewModel(
    INetworkService network,
    ISettingsService settings,
    IProfileService profiles,
    IPlanBuilder planBuilder,
    ITweakEngine engine,
    IBackupService backups,
    ILatencyMonitor monitor,
    IElevationService elevation,
    ISnackbarService snackbar,
    IContentDialogService dialogs
) : PageViewModel
{
    private TweakContext? _context;
    private bool _loading;
    private CancellationTokenSource? _runCts;

    [ObservableProperty]
    private bool _smartPackets = true;

    [ObservableProperty]
    private int _latency = 75;

    [ObservableProperty]
    private int _responsiveness = 60;

    [ObservableProperty]
    private bool _stableConnection = true;

    [ObservableProperty]
    private bool _includeAdvanced;

    [ObservableProperty]
    private NamedOption<TuningLevel>? _selectedTuning;

    [ObservableProperty]
    private NamedOption<ConnectionType>? _selectedConnection;

    [ObservableProperty]
    private NetworkAdapterInfo? _selectedAdapter;

    [ObservableProperty]
    private ObservableCollection<NetworkAdapterInfo> _adapters = [];

    [ObservableProperty]
    private ObservableCollection<PlanGroup> _planGroups = [];

    [ObservableProperty]
    private int _planCount;

    [ObservableProperty]
    private int _pendingCount;

    [ObservableProperty]
    private int _restartCount;

    [ObservableProperty]
    private bool _isElevated = true;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _restartRequired;

    [ObservableProperty]
    private ObservableCollection<string> _log = [];

    [ObservableProperty]
    private bool _measureBeforeAndAfter;

    [ObservableProperty]
    private string _benchmarkEstimate = string.Empty;

    [ObservableProperty]
    private string _benchmarkTarget = string.Empty;

    [ObservableProperty]
    private bool _hasBenchmarkTarget;

    [ObservableProperty]
    private BenchmarkComparison? _comparison;

    [ObservableProperty]
    private bool _advancedMode;

    public IReadOnlyList<NamedOption<TuningLevel>> TuningOptions { get; } =
        [
            .. Enum.GetValues<TuningLevel>().Select(v => new NamedOption<TuningLevel>(v.ToDisplay(), v)),
        ];

    public IReadOnlyList<NamedOption<ConnectionType>> ConnectionOptions { get; } =
        [
            .. Enum.GetValues<ConnectionType>().Select(v => new NamedOption<ConnectionType>(v.ToDisplay(), v)),
        ];

    private IReadOnlyList<PlanItem> _plan = [];

    public override async Task OnNavigatedToAsync()
    {
        IsElevated = elevation.IsElevated;
        await LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        _loading = true;
        IsBusy = true;
        BusyMessage = "Reading adapters and current settings";

        try
        {
            var profile = profiles.Active;
            var adapters = network.GetAdapters().Where(a => a.IsUp).ToList();
            var adapter = network.GetPreferredAdapter(profile.AdapterId ?? settings.Current.PreferredAdapterId);

            OnUi(() =>
            {
                Adapters = [.. adapters];
                SelectedAdapter = adapter is null ? null : adapters.FirstOrDefault(a => a.Id == adapter.Id);
                SmartPackets = profile.SmartPackets;
                Latency = profile.Latency;
                Responsiveness = profile.Responsiveness;
                StableConnection = profile.StableConnection;
                IncludeAdvanced = profile.IncludeAdvanced;
                SelectedTuning = TuningOptions.FirstOrDefault(o => o.Value == profile.Tuning) ?? TuningOptions[2];
                SelectedConnection =
                    ConnectionOptions.FirstOrDefault(o => o.Value == profile.Connection) ?? ConnectionOptions[0];
                AdvancedMode = settings.Current.AdvancedMode;
                MeasureBeforeAndAfter = settings.Current.MeasureBeforeAndAfter;
                RefreshBenchmarkEstimate();
            });

            _context = await engine.CreateContextAsync(adapter).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OnUi(() => snackbar.Show("Load failed", ex.Message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(6)));
        }
        finally
        {
            _loading = false;
            IsBusy = false;
            BusyMessage = string.Empty;
        }

        RebuildPlan();
    }

    private OptimizationProfile CurrentProfile()
    {
        var profile = profiles.Active;

        profile.SmartPackets = SmartPackets;
        profile.Latency = Latency;
        profile.Responsiveness = Responsiveness;
        profile.StableConnection = StableConnection;
        profile.IncludeAdvanced = IncludeAdvanced;
        profile.Tuning = SelectedTuning?.Value ?? TuningLevel.Restricted;
        profile.Connection = SelectedConnection?.Value ?? ConnectionType.Fiber;
        profile.AdapterId = SelectedAdapter?.Id;

        return profile;
    }

    private void RebuildPlan()
    {
        if (_loading || _context is null)
        {
            return;
        }

        var profile = CurrentProfile();
        profiles.SaveActive();

        _plan = planBuilder.Build(profile, _context);

        var groups = _plan
            .GroupBy(p => p.Tweak.Category)
            .OrderBy(g => g.Key)
            .Select(g => new PlanGroup(g.Key.ToDisplay(), g))
            .ToList();

        OnUi(() =>
        {
            PlanGroups = [.. groups];
            PlanCount = _plan.Count;
            PendingCount = _plan.Count(p => p.Status != TweakStatus.Optimized);
            RestartCount = _plan.Count(p => p.Tweak.RequiresRestart && p.Status != TweakStatus.Optimized);
        });
    }

    private PingTarget? PrimaryTarget()
    {
        var config = settings.Current;

        return config.Targets.FirstOrDefault(t => t.IsPrimary && t.Enabled)
            ?? config.Targets.FirstOrDefault(t => t.Enabled);
    }

    private int SampleCount() => Math.Max(BenchmarkComparison.MinimumSamples, settings.Current.BenchmarkSamples);

    private void RefreshBenchmarkEstimate()
    {
        var target = PrimaryTarget();

        HasBenchmarkTarget = target is not null;

        if (target is null)
        {
            BenchmarkTarget = "no server to measure against";
            BenchmarkEstimate = "Add a server on the Watch my ping page first.";
            return;
        }

        var samples = SampleCount();
        var seconds = LatencyMonitor.EstimateCaptureSeconds(target, samples, settings.Current.PingIntervalMs) * 2;

        BenchmarkTarget = $"{target.Name}  ({target.Host})";
        BenchmarkEstimate =
            $"{samples} pings before and {samples} after, so roughly {DurationText(seconds)} on top of the changes themselves.";
    }

    private static string DurationText(int seconds) =>
        seconds < 90 ? $"{seconds} seconds" : $"{Math.Round(seconds / 60d)} minutes";

    private async Task<BenchmarkRun> CaptureAsync(PingTarget target, string stage, CancellationToken ct)
    {
        var samples = SampleCount();

        var progress = new Progress<int>(done =>
            OnUi(() =>
            {
                Progress = done * 100d / samples;
                ProgressText = $"{stage}   {done} of {samples}";
            })
        );

        OnUi(() =>
        {
            Progress = 0;
            ProgressText = stage;
        });

        return await monitor
            .CaptureAsync(target, samples, settings.Current.PingIntervalMs, settings.Current.PingTimeoutMs, progress, ct)
            .ConfigureAwait(false);
    }

    partial void OnMeasureBeforeAndAfterChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        settings.Current.MeasureBeforeAndAfter = value;
        settings.Save();
        RefreshBenchmarkEstimate();
    }

    partial void OnSmartPacketsChanged(bool value) => RebuildPlan();

    partial void OnLatencyChanged(int value) => RebuildPlan();

    partial void OnResponsivenessChanged(int value) => RebuildPlan();

    partial void OnStableConnectionChanged(bool value) => RebuildPlan();

    partial void OnIncludeAdvancedChanged(bool value) => RebuildPlan();

    partial void OnSelectedTuningChanged(NamedOption<TuningLevel>? value) => RebuildPlan();

    partial void OnSelectedConnectionChanged(NamedOption<ConnectionType>? value) => RebuildPlan();

    partial void OnSelectedAdapterChanged(NetworkAdapterInfo? value)
    {
        if (_loading || value is null)
        {
            return;
        }

        settings.Current.PreferredAdapterId = value.Id;
        settings.Save();

        _ = ReloadContextAsync();
    }

    private async Task ReloadContextAsync()
    {
        try
        {
            _context = await engine.CreateContextAsync(SelectedAdapter).ConfigureAwait(false);
            RebuildPlan();
        }
        catch
        {
        }
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (_plan.Count == 0)
        {
            snackbar.Show(
                "Nothing to do",
                "Raise a slider or enable smart packets to build a plan.",
                ControlAppearance.Caution,
                null,
                TimeSpan.FromSeconds(4)
            );
            return;
        }

        if (!elevation.IsElevated)
        {
            snackbar.Show(
                "Administrator required",
                "Project-Agil needs elevation to write these settings.",
                ControlAppearance.Danger,
                null,
                TimeSpan.FromSeconds(6)
            );
            return;
        }

        if (settings.Current.ConfirmBeforeApply)
        {
            var confirmed = await dialogs.ConfirmAsync(
                "Apply optimization",
                $"{_plan.Count} settings will be changed on {SelectedAdapter?.Name ?? "this system"}.\n\n"
                    + "The previous value of every single one is saved to a restore point first, so you can undo all of it from the Restore points page.",
                "Apply now"
            );

            if (!confirmed)
            {
                return;
            }
        }

        IsRunning = true;
        Progress = 0;
        Log.Clear();
        Comparison = null;

        var profile = CurrentProfile();
        profiles.SaveActive();

        var progress = new Progress<EngineProgress>(p =>
        {
            Progress = p.Total == 0 ? 0 : p.Current * 100d / p.Total;
            ProgressText = $"{p.Current} of {p.Total}   {p.Message}";
        });

        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;
        var phase = RunPhase.BeforeMeasurement;

        try
        {
            var target = PrimaryTarget();
            var measuring = MeasureBeforeAndAfter && target is not null;
            BenchmarkRun? before = null;

            if (measuring)
            {
                before = await CaptureAsync(target!, "Measuring your ping before the changes", ct)
                    .ConfigureAwait(false);
            }

            phase = RunPhase.Applying;

            var result = await engine.ApplyAsync(_plan, profile, SelectedAdapter, progress, ct).ConfigureAwait(false);

            phase = RunPhase.AfterMeasurement;

            OnUi(() =>
            {
                foreach (var line in result.Log)
                {
                    Log.Add(line);
                }

                RestartRequired = result.RestartRequired;
                ProgressText = result.Headline;

                snackbar.Show(
                    result.NotConfirmed == 0 ? "Optimization applied" : "Applied with warnings",
                    result.NotConfirmed == 0
                        ? $"{result.Verified} settings confirmed, {result.Skipped} not available on this system. A restore point was saved."
                        : $"{result.Verified} settings confirmed, but {result.NotConfirmed} were accepted by Windows and then read back unchanged. See the log below.",
                    result.NotConfirmed == 0 ? ControlAppearance.Success : ControlAppearance.Caution,
                    null,
                    TimeSpan.FromSeconds(result.NotConfirmed == 0 ? 6 : 9)
                );
            });

            if (before is not null)
            {
                var after = await CaptureAsync(target!, "Measuring your ping after the changes", ct)
                    .ConfigureAwait(false);

                var comparison = new BenchmarkComparison
                {
                    Before = before,
                    After = after,
                    IncompleteBecauseRestartPending = result.PendingRestart > 0,
                };

                if (result.Snapshot is not null)
                {
                    result.Snapshot.Benchmark = comparison;
                    backups.Save(result.Snapshot);
                }

                OnUi(() =>
                {
                    Comparison = comparison;
                    ProgressText = $"{result.Headline}   {comparison.VerdictLabel}";
                });
            }

            await ReloadContextAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            OnUi(() =>
            {
                ProgressText = string.Empty;
                snackbar.Show(
                    "Stopped",
                    phase switch
                    {
                        RunPhase.BeforeMeasurement => "Stopped before anything was changed.",
                        RunPhase.Applying =>
                            "Stopped part way through. Whatever had already been written was saved to a restore point, so you can put it back from the Undo points page.",
                        _ =>
                            "The changes were applied and a restore point was saved. Only the measurement was stopped early.",
                    },
                    ControlAppearance.Caution,
                    null,
                    TimeSpan.FromSeconds(8)
                );
            });

            await ReloadContextAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OnUi(() => snackbar.Show("Apply failed", ex.Message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(6)));
        }
        finally
        {
            _runCts?.Dispose();
            _runCts = null;
            IsRunning = false;
            Progress = 0;
        }
    }

    [RelayCommand]
    private void CancelRun() => _runCts?.Cancel();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(false);

    [RelayCommand]
    private void ApplyPreset(string preset)
    {
        _loading = true;

        switch (preset)
        {
            case "safe":
                SmartPackets = true;
                Latency = 45;
                Responsiveness = 35;
                StableConnection = true;
                IncludeAdvanced = false;
                SelectedTuning = TuningOptions.First(o => o.Value == TuningLevel.Normal);
                break;
            case "balanced":
                SmartPackets = true;
                Latency = 70;
                Responsiveness = 60;
                StableConnection = true;
                IncludeAdvanced = false;
                SelectedTuning = TuningOptions.First(o => o.Value == TuningLevel.Restricted);
                break;
            case "pvp":
                SmartPackets = true;
                Latency = 92;
                Responsiveness = 85;
                StableConnection = true;
                IncludeAdvanced = true;
                SelectedTuning = TuningOptions.First(o => o.Value == TuningLevel.HighlyRestricted);
                break;
            case "unstable":
                SmartPackets = true;
                Latency = 60;
                Responsiveness = 55;
                StableConnection = false;
                IncludeAdvanced = false;
                SelectedTuning = TuningOptions.First(o => o.Value == TuningLevel.Restricted);
                break;
        }

        _loading = false;
        RebuildPlan();
    }
}
