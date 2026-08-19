using ProjectAgil.Models;

namespace ProjectAgil.Services;

public sealed record EngineProgress(int Current, int Total, string Message);

public sealed class ApplyResult
{
    public int Applied { get; init; }

    public int Skipped { get; init; }

    public bool RestartRequired { get; init; }

    public BackupSnapshot? Snapshot { get; init; }

    public List<string> Log { get; init; } = [];
}

public interface ITweakEngine
{
    Task<TweakContext> CreateContextAsync(NetworkAdapterInfo? adapter, CancellationToken ct = default);

    Task<ApplyResult> ApplyAsync(
        IReadOnlyList<PlanItem> items,
        OptimizationProfile profile,
        NetworkAdapterInfo? adapter,
        IProgress<EngineProgress>? progress = null,
        CancellationToken ct = default
    );

    Task<int> RestoreAsync(
        BackupSnapshot snapshot,
        IProgress<EngineProgress>? progress = null,
        CancellationToken ct = default
    );

    Task<int> RestoreAllAsync(IProgress<EngineProgress>? progress = null, CancellationToken ct = default);
}

public sealed class TweakEngine(
    ITweakCatalog catalog,
    IRegistryService registry,
    IProcessRunner process,
    INetworkService network,
    IBackupService backups
) : ITweakEngine
{
    public async Task<TweakContext> CreateContextAsync(NetworkAdapterInfo? adapter, CancellationToken ct = default)
    {
        var state = await network.ReadStateAsync(adapter, ct).ConfigureAwait(false);

        return new TweakContext
        {
            Registry = registry,
            Process = process,
            Adapter = adapter,
            State = state,
        };
    }

    public async Task<ApplyResult> ApplyAsync(
        IReadOnlyList<PlanItem> items,
        OptimizationProfile profile,
        NetworkAdapterInfo? adapter,
        IProgress<EngineProgress>? progress = null,
        CancellationToken ct = default
    )
    {
        var log = new List<string>();
        var context = await CreateContextAsync(adapter, ct).ConfigureAwait(false);
        var working = new TweakContext
        {
            Registry = context.Registry,
            Process = context.Process,
            Adapter = context.Adapter,
            State = context.State,
            Log = message => log.Add(message),
        };

        var selected = items.Where(i => i.Selected).ToList();
        var snapshot = new BackupSnapshot
        {
            Label = $"{profile.Name} on {adapter?.Name ?? "system"}",
            ProfileName = profile.Name,
            AdapterName = adapter?.Name ?? string.Empty,
        };

        var applied = 0;
        var skipped = 0;
        var restart = false;

        for (var index = 0; index < selected.Count; index++)
        {
            ct.ThrowIfCancellationRequested();

            var item = selected[index];
            progress?.Report(new EngineProgress(index + 1, selected.Count, item.Tweak.Name));

            try
            {
                var entry = await item.Tweak.ApplyAsync(working, item.DesiredValue, ct).ConfigureAwait(false);

                if (entry is null)
                {
                    skipped++;
                    item.Status = TweakStatus.Unsupported;
                    continue;
                }

                snapshot.Entries.Add(entry);
                applied++;
                item.Status = TweakStatus.Optimized;
                item.CurrentValue = item.DesiredValue;
                restart |= item.Tweak.RequiresRestart;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                skipped++;
                item.Status = TweakStatus.Unsupported;
                log.Add($"{item.Tweak.Name}: failed ({ex.Message})");
            }
        }

        if (snapshot.Entries.Count > 0)
        {
            backups.Save(snapshot);
        }

        return new ApplyResult
        {
            Applied = applied,
            Skipped = skipped,
            RestartRequired = restart,
            Snapshot = snapshot.Entries.Count > 0 ? snapshot : null,
            Log = log,
        };
    }

    public async Task<int> RestoreAsync(
        BackupSnapshot snapshot,
        IProgress<EngineProgress>? progress = null,
        CancellationToken ct = default
    )
    {
        var adapters = network.GetAdapters();
        var restored = 0;

        for (var index = snapshot.Entries.Count - 1; index >= 0; index--)
        {
            ct.ThrowIfCancellationRequested();

            var entry = snapshot.Entries[index];
            progress?.Report(
                new EngineProgress(snapshot.Entries.Count - index, snapshot.Entries.Count, entry.TweakName)
            );

            var tweak = catalog.Find(entry.TweakId);
            if (tweak is null)
            {
                continue;
            }

            var adapter = entry.AdapterId is null
                ? null
                : adapters.FirstOrDefault(a => a.Id == entry.AdapterId)
                    ?? adapters.FirstOrDefault(a => a.Name == entry.AdapterName);

            var context = new TweakContext
            {
                Registry = registry,
                Process = process,
                Adapter = adapter,
            };

            try
            {
                await tweak.RestoreAsync(context, entry, ct).ConfigureAwait(false);
                restored++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }

        snapshot.Restored = true;
        backups.Save(snapshot);

        return restored;
    }

    public async Task<int> RestoreAllAsync(IProgress<EngineProgress>? progress = null, CancellationToken ct = default)
    {
        var total = 0;

        foreach (var snapshot in backups.LoadAll().Where(s => !s.Restored))
        {
            total += await RestoreAsync(snapshot, progress, ct).ConfigureAwait(false);
        }

        return total;
    }
}
