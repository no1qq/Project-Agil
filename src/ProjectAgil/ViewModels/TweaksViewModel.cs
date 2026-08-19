using System.Windows.Data;
using ProjectAgil.Models;
using ProjectAgil.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ProjectAgil.ViewModels;

public partial class TweakRow(Tweak tweak) : ObservableObject
{
    [ObservableProperty]
    private string? _current;

    [ObservableProperty]
    private TweakStatus _status = TweakStatus.Unknown;

    [ObservableProperty]
    private bool _excluded;

    [ObservableProperty]
    private string _target = string.Empty;

    public Tweak Tweak { get; } = tweak;

    public string Id => Tweak.Id;

    public string Name => Tweak.Name;

    public string Description => Tweak.Description;

    public string Impact => Tweak.Impact;

    public string CategoryDisplay => Tweak.Category.ToDisplay();

    public string RiskDisplay => Tweak.Risk.ToDisplay();

    public string Recommended => Tweak.OptimizedValue;

    public bool RequiresRestart => Tweak.RequiresRestart;

    public string CurrentDisplay => Current ?? "not set";

    partial void OnCurrentChanged(string? value) => OnPropertyChanged(nameof(CurrentDisplay));
}

public partial class TweaksViewModel(
    ITweakCatalog catalog,
    INetworkService network,
    ISettingsService settings,
    IProfileService profiles,
    ITweakEngine engine,
    IBackupService backups,
    ISnackbarService snackbar
) : PageViewModel
{
    private TweakContext? _context;
    private NetworkAdapterInfo? _adapter;

    [ObservableProperty]
    private ObservableCollection<TweakRow> _rows = [];

    [ObservableProperty]
    private System.ComponentModel.ICollectionView? _view;

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private bool _onlyPending;

    [ObservableProperty]
    private int _optimizedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private string _adapterName = string.Empty;

    public IReadOnlyList<string> Categories { get; } =
        ["All", .. Enum.GetValues<TweakCategory>().Select(c => c.ToDisplay())];

    public override async Task OnNavigatedToAsync() => await RefreshAsync().ConfigureAwait(false);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        BusyMessage = "Reading every setting";

        try
        {
            _adapter = network.GetPreferredAdapter(settings.Current.PreferredAdapterId);
            _context = await engine.CreateContextAsync(_adapter).ConfigureAwait(false);

            var excluded = profiles.Active.ExcludedTweaks;
            var rows = new List<TweakRow>();

            foreach (var tweak in catalog.All)
            {
                var row = new TweakRow(tweak) { Excluded = excluded.Contains(tweak.Id, StringComparer.OrdinalIgnoreCase) };

                if (tweak.NeedsAdapter && _adapter is null)
                {
                    row.Status = TweakStatus.Unsupported;
                    row.Target = "no adapter selected";
                }
                else
                {
                    row.Current = tweak.Read(_context);
                    row.Status = row.Current is null
                        ? TweakStatus.NotOptimized
                        : tweak.Matches(row.Current, tweak.OptimizedValue)
                            ? TweakStatus.Optimized
                            : TweakStatus.NotOptimized;
                    row.Target = tweak.Target(_context);
                }

                rows.Add(row);
            }

            OnUi(() =>
            {
                AdapterName = _adapter?.Name ?? "no adapter";
                Rows = [.. rows];
                TotalCount = rows.Count;
                OptimizedCount = rows.Count(r => r.Status == TweakStatus.Optimized);

                var view = CollectionViewSource.GetDefaultView(Rows);
                view.Filter = Matches;
                View = view;
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

    private bool Matches(object item)
    {
        if (item is not TweakRow row)
        {
            return false;
        }

        if (OnlyPending && row.Status == TweakStatus.Optimized)
        {
            return false;
        }

        if (!SelectedCategory.Equals("All", StringComparison.OrdinalIgnoreCase)
            && !row.CategoryDisplay.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Search))
        {
            return true;
        }

        return row.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || row.Description.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || row.Target.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || row.Id.Contains(Search, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnSearchChanged(string value) => View?.Refresh();

    partial void OnSelectedCategoryChanged(string value) => View?.Refresh();

    partial void OnOnlyPendingChanged(bool value) => View?.Refresh();

    [RelayCommand]
    private async Task ApplyOneAsync(TweakRow? row)
    {
        if (row is null || _context is null)
        {
            return;
        }

        IsBusy = true;
        BusyMessage = $"Applying {row.Name}";

        try
        {
            var item = new PlanItem
            {
                Tweak = row.Tweak,
                DesiredValue = row.Tweak.OptimizedValue,
                Reason = "Applied individually from the Tweaks page",
                CurrentValue = row.Current,
            };

            var result = await engine
                .ApplyAsync([item], profiles.Active, _adapter)
                .ConfigureAwait(false);

            OnUi(() =>
            {
                if (result.Applied > 0)
                {
                    row.Current = row.Tweak.OptimizedValue;
                    row.Status = TweakStatus.Optimized;
                    OptimizedCount = Rows.Count(r => r.Status == TweakStatus.Optimized);

                    snackbar.Show(
                        "Applied",
                        $"{row.Name} is now set to {row.Tweak.OptimizedValue}.",
                        ControlAppearance.Success,
                        null,
                        TimeSpan.FromSeconds(4)
                    );
                }
                else
                {
                    row.Status = TweakStatus.Unsupported;
                    snackbar.Show(
                        "Not available",
                        $"{row.Name} is not exposed on this system.",
                        ControlAppearance.Caution,
                        null,
                        TimeSpan.FromSeconds(5)
                    );
                }
            });
        }
        catch (Exception ex)
        {
            OnUi(() => snackbar.Show("Apply failed", ex.Message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(6)));
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task RevertOneAsync(TweakRow? row)
    {
        if (row is null)
        {
            return;
        }

        var snapshot = backups
            .LoadAll()
            .FirstOrDefault(s => !s.Restored && s.Entries.Any(e => e.TweakId == row.Id));

        if (snapshot is null)
        {
            snackbar.Show(
                "No restore point",
                $"Project-Agil has no saved previous value for {row.Name}.",
                ControlAppearance.Caution,
                null,
                TimeSpan.FromSeconds(5)
            );
            return;
        }

        var entry = snapshot.Entries.First(e => e.TweakId == row.Id);
        var single = new BackupSnapshot
        {
            Id = snapshot.Id,
            Label = snapshot.Label,
            ProfileName = snapshot.ProfileName,
            AdapterName = snapshot.AdapterName,
            Entries = [entry],
        };

        _ = await engine.RestoreAsync(single).ConfigureAwait(false);

        snapshot.Entries.Remove(entry);
        snapshot.Restored = snapshot.Entries.Count == 0;
        backups.Save(snapshot);

        await RefreshAsync().ConfigureAwait(false);

        OnUi(() =>
            snackbar.Show(
                "Reverted",
                $"{row.Name} was put back to {entry.PreviousDisplay}.",
                ControlAppearance.Success,
                null,
                TimeSpan.FromSeconds(4)
            )
        );
    }

    [RelayCommand]
    private void ToggleExclusion(TweakRow? row)
    {
        if (row is null)
        {
            return;
        }

        var excluded = profiles.Active.ExcludedTweaks;

        if (row.Excluded)
        {
            _ = excluded.RemoveAll(id => id.Equals(row.Id, StringComparison.OrdinalIgnoreCase));
            row.Excluded = false;
        }
        else
        {
            excluded.Add(row.Id);
            row.Excluded = true;
        }

        profiles.SaveActive();
    }
}
