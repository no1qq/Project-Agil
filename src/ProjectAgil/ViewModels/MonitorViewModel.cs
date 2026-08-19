using System.IO;
using Microsoft.Win32;
using ProjectAgil.Models;
using ProjectAgil.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ProjectAgil.ViewModels;

public partial class MonitorRow(PingTarget target) : ObservableObject
{
    [ObservableProperty]
    private LatencyStats? _stats;

    [ObservableProperty]
    private bool _isPrimary = target.IsPrimary;

    public PingTarget Target { get; } = target;

    public string Name => Target.Name;

    public string Host => Target.Host;

    public string KindDisplay => Target.KindDisplay;

    public bool IsMinecraft => Target.Kind == PingKind.Minecraft;

    public string CurrentDisplay => Stats?.CurrentDisplay ?? "-";

    public string AverageDisplay => Stats?.AverageDisplay ?? "-";

    public string JitterDisplay => Stats?.JitterDisplay ?? "-";

    public string LossDisplay => Stats?.LossDisplay ?? "-";

    public string RangeDisplay => Stats?.RangeDisplay ?? "-";

    public string RefusedNote => Stats?.RefusedNote ?? string.Empty;

    public string GradeLabel => Stats is null || Stats.Sent == 0 ? "measuring" : Stats.GradeLabel;

    public int Grade => Stats?.Grade ?? 0;

    public int Sent => Stats?.Sent ?? 0;

    partial void OnStatsChanged(LatencyStats? value)
    {
        OnPropertyChanged(nameof(CurrentDisplay));
        OnPropertyChanged(nameof(AverageDisplay));
        OnPropertyChanged(nameof(JitterDisplay));
        OnPropertyChanged(nameof(LossDisplay));
        OnPropertyChanged(nameof(RangeDisplay));
        OnPropertyChanged(nameof(RefusedNote));
        OnPropertyChanged(nameof(GradeLabel));
        OnPropertyChanged(nameof(Grade));
        OnPropertyChanged(nameof(Sent));
    }
}

public partial class MonitorViewModel(ISettingsService settings, ILatencyMonitor monitor, ISnackbarService snackbar)
    : PageViewModel
{
    private bool _hooked;

    [ObservableProperty]
    private ObservableCollection<MonitorRow> _rows = [];

    [ObservableProperty]
    private MonitorRow? _selected;

    [ObservableProperty]
    private IReadOnlyList<double> _history = [];

    [ObservableProperty]
    private string _newTargetName = string.Empty;

    [ObservableProperty]
    private string _newTargetHost = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewTargetHostPlaceholder))]
    [NotifyPropertyChangedFor(nameof(NewTargetSummary))]
    private bool _newTargetIsMinecraft = true;

    [ObservableProperty]
    private int _intervalMs = 500;

    public string NewTargetHostPlaceholder =>
        NewTargetIsMinecraft ? "mc.hypixel.net" : "1.1.1.1";

    public string NewTargetSummary =>
        NewTargetIsMinecraft
            ? "Minecraft server on: the address is measured the way the game measures it, so the number matches your multiplayer screen."
            : "Minecraft server off: the address gets a plain network ping, which is all that works on something that is not a game server.";

    public override Task OnNavigatedToAsync()
    {
        if (!_hooked)
        {
            monitor.Updated += OnUpdated;
            _hooked = true;
        }

        IntervalMs = settings.Current.PingIntervalMs;
        BuildRows();

        if (!monitor.IsRunning)
        {
            Restart();
        }

        return Task.CompletedTask;
    }

    private void BuildRows()
    {
        var rows = InPreferredOrder(settings.Current.Targets).Select(t => new MonitorRow(t)).ToList();
        var selectedHost = Selected?.Host;

        Rows = [.. rows];
        Selected = rows.FirstOrDefault(r => r.Host == selectedHost)
            ?? rows.FirstOrDefault(r => r.IsPrimary)
            ?? rows.FirstOrDefault();

        Apply(monitor.Snapshot());
    }

    private void Apply(IReadOnlyList<LatencyStats> stats)
    {
        foreach (var row in Rows)
        {
            row.Stats = stats.FirstOrDefault(s => s.Host.Equals(row.Host, StringComparison.OrdinalIgnoreCase));
        }

        if (Selected is not null)
        {
            History = monitor.History(Selected.Host);
        }
    }

    private void OnUpdated(object? sender, IReadOnlyList<LatencyStats> stats) => OnUi(() => Apply(stats));

    partial void OnSelectedChanged(MonitorRow? value)
    {
        if (value is not null)
        {
            History = monitor.History(value.Host);
        }
    }

    private void Persist()
    {
        settings.Current.Targets = [.. Rows.Select(r => r.Target)];
        settings.Current.PingIntervalMs = Math.Clamp(IntervalMs, 100, 5000);
        settings.Save();
    }

    private static IEnumerable<PingTarget> InPreferredOrder(IEnumerable<PingTarget> targets) =>
        targets.OrderBy(t => t.Kind == PingKind.Minecraft ? 0 : 1);

    private void Restart()
    {
        Persist();

        var config = settings.Current;
        monitor.Start(config.Targets, config.PingIntervalMs, config.PingTimeoutMs, config.HistorySize);
        monitor.Resume();
    }

    [RelayCommand]
    private void ResetStats()
    {
        monitor.Reset();
        History = [];
        Apply(monitor.Snapshot());
    }

    [RelayCommand]
    private void AddTarget()
    {
        if (string.IsNullOrWhiteSpace(NewTargetHost))
        {
            return;
        }

        var host = NewTargetHost.Trim();
        var port = 25565;

        var colon = host.LastIndexOf(':');
        if (colon > 0 && int.TryParse(host[(colon + 1)..], out var parsed) && parsed is > 0 and <= 65535)
        {
            port = parsed;
            host = host[..colon];
        }

        if (Rows.Any(r => r.Host.Equals(host, StringComparison.OrdinalIgnoreCase)))
        {
            snackbar.Show(
                "Already on the list",
                $"{host} is already being watched.",
                ControlAppearance.Caution,
                null,
                TimeSpan.FromSeconds(4)
            );
            return;
        }

        var name = string.IsNullOrWhiteSpace(NewTargetName) ? host : NewTargetName.Trim();

        var row = new MonitorRow(
            new PingTarget
            {
                Name = name,
                Host = host,
                Port = port,
                Kind = NewTargetIsMinecraft ? PingKind.Minecraft : PingKind.Icmp,
                Enabled = true,
            }
        );

        if (NewTargetIsMinecraft)
        {
            var index = 0;

            for (var i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].IsMinecraft)
                {
                    index = i + 1;
                }
            }

            Rows.Insert(index, row);
        }
        else
        {
            Rows.Add(row);
        }

        Selected = row;

        NewTargetName = string.Empty;
        NewTargetHost = string.Empty;

        Restart();
    }

    [RelayCommand]
    private void RemoveTarget(MonitorRow? row)
    {
        if (row is null || Rows.Count <= 1)
        {
            return;
        }

        _ = Rows.Remove(row);

        if (Selected == row)
        {
            Selected = Rows.FirstOrDefault();
        }

        Restart();
    }

    [RelayCommand]
    private void MakePrimary(MonitorRow? row)
    {
        if (row is null)
        {
            return;
        }

        foreach (var item in Rows)
        {
            item.Target.IsPrimary = item == row;
            item.IsPrimary = item == row;
        }

        Persist();

        snackbar.Show(
            "Pinned",
            $"{row.Name} now shows on the dashboard.",
            ControlAppearance.Success,
            null,
            TimeSpan.FromSeconds(4)
        );
    }

    [RelayCommand]
    private void ExportCsv()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV file|*.csv",
            FileName = $"agil-latency-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, monitor.ExportCsv());
            snackbar.Show("Saved", dialog.FileName, ControlAppearance.Success, null, TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            snackbar.Show("Could not save", ex.Message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(6));
        }
    }
}
