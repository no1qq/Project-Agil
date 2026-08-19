using ProjectAgil.Models;

namespace ProjectAgil.Services;

public sealed record EngineProgress(int Current, int Total, string Message);

public sealed class ApplyResult
{
    public int Applied { get; init; }

    public int Verified { get; init; }

    public int PendingRestart { get; init; }

    public int NotConfirmed { get; init; }

    public int Skipped { get; init; }

    public bool RestartRequired { get; init; }

    public BackupSnapshot? Snapshot { get; init; }

    public List<string> Log { get; init; } = [];

    public string Headline =>
        Applied == 0
            ? "Nothing was changed"
            : NotConfirmed == 0
                ? $"{Verified} of {Applied} changes confirmed"
                : $"{Verified} of {Applied} changes confirmed, {NotConfirmed} did not stick";
}

public sealed class RestoreResult
{
    public int Restored { get; init; }

    public int Failed { get; init; }

    public bool Complete { get; init; }

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

    Task<RestoreResult> RestoreAsync(
        BackupSnapshot snapshot,
        IProgress<EngineProgress>? progress = null,
        CancellationToken ct = default
    );

    Task<RestoreResult> RestoreEntryAsync(BackupSnapshot snapshot, BackupEntry entry, CancellationToken ct = default);

    Task<RestoreResult> RestoreAllAsync(IProgress<EngineProgress>? progress = null, CancellationToken ct = default);
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

        var written = new List<PlanItem>();
        var skipped = 0;
        var restart = false;

        try
        {
            for (var index = 0; index < selected.Count; index++)
            {
                ct.ThrowIfCancellationRequested();

                var item = selected[index];
                progress?.Report(new EngineProgress(index + 1, selected.Count + 1, item.Tweak.Name));

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
                    written.Add(item);
                    restart |= item.Tweak.RequiresRestart;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    skipped++;
                    item.Status = TweakStatus.Failed;
                    log.Add($"{item.Tweak.Name}: failed ({ex.Message})");
                }
            }
        }
        finally
        {
            if (snapshot.Entries.Count > 0)
            {
                backups.Save(snapshot);
            }
        }

        var verified = 0;
        var pendingRestart = 0;
        var notConfirmed = 0;

        if (written.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(
                new EngineProgress(selected.Count + 1, selected.Count + 1, "Checking what actually applied")
            );

            var fresh = await CreateContextAsync(adapter, ct).ConfigureAwait(false);

            foreach (var item in written)
            {
                if (item.Tweak.RequiresRestart)
                {
                    item.Status = TweakStatus.PendingRestart;
                    pendingRestart++;
                    continue;
                }

                var current = item.Tweak.Read(fresh);
                item.CurrentValue = current;

                if (current is not null && item.Tweak.Matches(current, item.DesiredValue))
                {
                    item.Status = TweakStatus.Optimized;
                    verified++;
                    continue;
                }

                item.Status = TweakStatus.NotConfirmed;
                notConfirmed++;
                log.Add(
                    $"{item.Tweak.Name}: reported success but reads back as {current ?? "not set"} instead of {item.DesiredValue}"
                );
            }
        }

        return new ApplyResult
        {
            Applied = written.Count,
            Verified = verified,
            PendingRestart = pendingRestart,
            NotConfirmed = notConfirmed,
            Skipped = skipped,
            RestartRequired = restart,
            Snapshot = snapshot.Entries.Count > 0 ? snapshot : null,
            Log = log,
        };
    }

    public Task<RestoreResult> RestoreAsync(
        BackupSnapshot snapshot,
        IProgress<EngineProgress>? progress = null,
        CancellationToken ct = default
    ) => RestoreCoreAsync(snapshot, [.. snapshot.Entries.Where(e => !e.Restored)], progress, ct);

    public Task<RestoreResult> RestoreEntryAsync(
        BackupSnapshot snapshot,
        BackupEntry entry,
        CancellationToken ct = default
    ) => RestoreCoreAsync(snapshot, entry.Restored ? [] : [entry], null, ct);

    private async Task<RestoreResult> RestoreCoreAsync(
        BackupSnapshot snapshot,
        IReadOnlyList<BackupEntry> targets,
        IProgress<EngineProgress>? progress,
        CancellationToken ct
    )
    {
        var adapters = network.GetAdapters();
        var log = new List<string>();
        var restored = 0;
        var failed = 0;

        var ordered = targets.OrderByDescending(snapshot.Entries.IndexOf).ToList();
        var total = ordered.Count;
        var done = 0;

        foreach (var entry in ordered)
        {
            ct.ThrowIfCancellationRequested();

            done++;
            progress?.Report(new EngineProgress(done, total, entry.TweakName));

            var tweak = catalog.Find(entry.TweakId);
            if (tweak is null)
            {
                entry.RestoreError = "this setting is no longer in the catalog";
                failed++;
                log.Add($"{entry.TweakName}: {entry.RestoreError}");
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
                Log = message => log.Add(message),
            };

            try
            {
                await tweak.RestoreAsync(context, entry, ct).ConfigureAwait(false);
                entry.Restored = true;
                entry.RestoreError = null;
                restored++;
            }
            catch (OperationCanceledException)
            {
                backups.Save(snapshot);
                throw;
            }
            catch (Exception ex)
            {
                entry.RestoreError = ex.Message;
                failed++;
                log.Add($"{entry.TweakName}: could not be put back ({ex.Message})");
            }
        }

        snapshot.Restored = snapshot.Entries.Count > 0 && snapshot.Entries.All(e => e.Restored);
        backups.Save(snapshot);

        return new RestoreResult
        {
            Restored = restored,
            Failed = failed,
            Complete = snapshot.Restored,
            Log = log,
        };
    }

    public async Task<RestoreResult> RestoreAllAsync(
        IProgress<EngineProgress>? progress = null,
        CancellationToken ct = default
    )
    {
        var restored = 0;
        var failed = 0;
        var complete = true;
        var log = new List<string>();

        foreach (var snapshot in backups.LoadAll().Where(s => !s.Restored))
        {
            var result = await RestoreAsync(snapshot, progress, ct).ConfigureAwait(false);

            restored += result.Restored;
            failed += result.Failed;
            complete &= result.Complete;
            log.AddRange(result.Log);
        }

        return new RestoreResult
        {
            Restored = restored,
            Failed = failed,
            Complete = complete,
            Log = log,
        };
    }
}
