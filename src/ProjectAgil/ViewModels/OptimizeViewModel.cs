using ProjectAgil.Helpers;
using ProjectAgil.Models;
using ProjectAgil.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ProjectAgil.ViewModels;

public sealed record NamedOption<T>(string Label, T Value);

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
    IElevationService elevation,
    ISnackbarService snackbar,
    IContentDialogService dialogs
) : PageViewModel
{
    private TweakContext? _context;
    private bool _loading;

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

        var profile = CurrentProfile();
        profiles.SaveActive();

        var progress = new Progress<EngineProgress>(p =>
        {
            Progress = p.Total == 0 ? 0 : p.Current * 100d / p.Total;
            ProgressText = $"{p.Current} of {p.Total}   {p.Message}";
        });

        try
        {
            var result = await engine.ApplyAsync(_plan, profile, SelectedAdapter, progress).ConfigureAwait(false);

            OnUi(() =>
            {
                foreach (var line in result.Log)
                {
                    Log.Add(line);
                }

                RestartRequired = result.RestartRequired;
                ProgressText = $"{result.Applied} applied, {result.Skipped} skipped";

                snackbar.Show(
                    "Optimization applied",
                    $"{result.Applied} settings changed. A restore point was saved.",
                    ControlAppearance.Success,
                    null,
                    TimeSpan.FromSeconds(6)
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
            IsRunning = false;
        }
    }

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
