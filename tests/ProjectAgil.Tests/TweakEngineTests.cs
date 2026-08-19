using System.Diagnostics.CodeAnalysis;
using Microsoft.Win32;
using ProjectAgil.Models;
using ProjectAgil.Services;
using Xunit;

namespace ProjectAgil.Tests;

public sealed class TweakEngineTests
{
    [Fact]
    public async Task AConfirmedWriteCountsAsVerified()
    {
        var tweak = Sticky("a");
        var harness = new Harness(tweak);

        var result = await harness.ApplyAsync(Plan(tweak, "on"));

        Assert.Equal(1, result.Applied);
        Assert.Equal(1, result.Verified);
        Assert.Equal(0, result.NotConfirmed);
    }

    [Fact]
    public async Task AWriteThatDoesNotReadBackIsReportedRatherThanClaimed()
    {
        var tweak = Ignored("a");
        var harness = new Harness(tweak);
        var plan = Plan(tweak, "on");

        var result = await harness.ApplyAsync(plan);

        Assert.Equal(1, result.Applied);
        Assert.Equal(0, result.Verified);
        Assert.Equal(1, result.NotConfirmed);
        Assert.Equal(TweakStatus.NotConfirmed, plan[0].Status);
        Assert.NotEmpty(result.Log);
    }

    [Fact]
    public async Task ASettingThatNeedsARestartIsNotClaimedAsConfirmed()
    {
        var tweak = new FakeTweak("a", sticks: false, requiresRestart: true);
        var harness = new Harness(tweak);
        var plan = Plan(tweak, "on");

        var result = await harness.ApplyAsync(plan);

        Assert.Equal(1, result.PendingRestart);
        Assert.Equal(0, result.NotConfirmed);
        Assert.Equal(TweakStatus.PendingRestart, plan[0].Status);
    }

    [Fact]
    public async Task StoppingPartWayThroughStillLeavesAnUndoPoint()
    {
        using var cts = new CancellationTokenSource();

        var first = Sticky("a");
        var second = Sticky("b");
        second.BeforeApply = cts.Cancel;
        var third = Sticky("c");

        var harness = new Harness(first, second, third);
        var plan = Plan((first, "on"), (second, "on"), (third, "on"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.ApplyAsync(plan, cts.Token));

        var saved = Assert.Single(harness.Backups.LoadAll());

        Assert.Equal(1, saved.EntryCount);
        Assert.False(saved.Restored);
    }

    [Fact]
    public async Task AFailedRevertLeavesTheUndoPointUsable()
    {
        var good = Sticky("a");
        var bad = Sticky("b");
        bad.RestoreFails = true;

        var harness = new Harness(good, bad);
        await harness.ApplyAsync(Plan((good, "on"), (bad, "on")));

        var snapshot = harness.Backups.LoadAll().Single();
        var result = await harness.Engine.RestoreAsync(snapshot);

        Assert.Equal(1, result.Restored);
        Assert.Equal(1, result.Failed);
        Assert.False(result.Complete);
        Assert.False(snapshot.Restored);
        Assert.True(snapshot.IsPartlyRestored);
        Assert.Equal("Partly reverted (1 of 2)", snapshot.StatusDisplay);

        var failed = snapshot.Entries.Single(e => e.TweakId == "b");

        Assert.False(failed.Restored);
        Assert.NotNull(failed.RestoreError);
    }

    [Fact]
    public async Task RetryingARevertOnlyTouchesWhatIsStillOutstanding()
    {
        var good = Sticky("a");
        var bad = Sticky("b");
        bad.RestoreFails = true;

        var harness = new Harness(good, bad);
        await harness.ApplyAsync(Plan((good, "on"), (bad, "on")));

        var snapshot = harness.Backups.LoadAll().Single();
        _ = await harness.Engine.RestoreAsync(snapshot);

        good.RestoreCount = 0;
        bad.RestoreFails = false;

        var second = await harness.Engine.RestoreAsync(snapshot);

        Assert.Equal(1, second.Restored);
        Assert.Equal(0, good.RestoreCount);
        Assert.True(snapshot.Restored);
        Assert.Equal("Reverted", snapshot.StatusDisplay);
    }

    [Fact]
    public async Task RevertingOneEntryLeavesTheRestOfTheUndoPointAlone()
    {
        var first = Sticky("a");
        var second = Sticky("b");

        var harness = new Harness(first, second);
        await harness.ApplyAsync(Plan((first, "on"), (second, "on")));

        var snapshot = harness.Backups.LoadAll().Single();
        var target = snapshot.Entries.Single(e => e.TweakId == "a");

        _ = await harness.Engine.RestoreEntryAsync(snapshot, target);

        var stored = harness.Backups.LoadAll().Single();

        Assert.Equal(2, stored.EntryCount);
        Assert.True(stored.Entries.Single(e => e.TweakId == "a").Restored);
        Assert.False(stored.Entries.Single(e => e.TweakId == "b").Restored);
        Assert.False(stored.Restored);
    }

    [Fact]
    public async Task AnUnreadableSettingIsNeverWritten()
    {
        var tweak = Sticky("a");
        tweak.Current = null;

        var harness = new Harness(tweak);
        var plan = Plan(tweak, "on");

        var result = await harness.ApplyAsync(plan);

        Assert.Equal(0, result.Applied);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, tweak.ApplyCount);
        Assert.Empty(harness.Backups.LoadAll());
    }

    private static FakeTweak Sticky(string id) => new(id, sticks: true);

    private static FakeTweak Ignored(string id) => new(id, sticks: false);

    private static List<PlanItem> Plan(Tweak tweak, string value) => Plan((tweak, value));

    private static List<PlanItem> Plan(params (Tweak Tweak, string Value)[] items) =>
        [
            .. items.Select(i => new PlanItem
            {
                Tweak = i.Tweak,
                DesiredValue = i.Value,
                Reason = "test",
            }),
        ];

    private sealed class Harness
    {
        public Harness(params Tweak[] tweaks)
        {
            Catalog = new FakeCatalog(tweaks);
            Backups = new FakeBackups();
            Engine = new TweakEngine(Catalog, new FakeRegistry(), new FakeProcess(), new FakeNetwork(), Backups);
        }

        public FakeCatalog Catalog { get; }

        public FakeBackups Backups { get; }

        public TweakEngine Engine { get; }

        public Task<ApplyResult> ApplyAsync(IReadOnlyList<PlanItem> plan, CancellationToken ct = default) =>
            Engine.ApplyAsync(plan, new OptimizationProfile { Name = "test" }, null, null, ct);
    }

    private sealed class FakeTweak : Tweak
    {
        [SetsRequiredMembers]
        public FakeTweak(string id, bool sticks, bool requiresRestart = false)
        {
            Id = id;
            Name = id;
            Description = id;
            OptimizedValue = "on";
            RequiresRestart = requiresRestart;
            Sticks = sticks;
        }

        public bool Sticks { get; }

        public bool RestoreFails { get; set; }

        public string? Current { get; set; } = "off";

        public Action? BeforeApply { get; set; }

        public int ApplyCount { get; set; }

        public int RestoreCount { get; set; }

        public override string Kind => "fake";

        public override string Target(TweakContext ctx) => Id;

        public override string? Read(TweakContext ctx) => Current;

        public override Task<BackupEntry?> ApplyAsync(TweakContext ctx, string value, CancellationToken ct)
        {
            BeforeApply?.Invoke();
            ct.ThrowIfCancellationRequested();

            var previous = Read(ctx);
            if (previous is null)
            {
                return Task.FromResult<BackupEntry?>(null);
            }

            ApplyCount++;

            if (Sticks)
            {
                Current = value;
            }

            return Task.FromResult<BackupEntry?>(
                new BackupEntry
                {
                    TweakId = Id,
                    TweakName = Name,
                    Kind = Kind,
                    Target = Id,
                    PreviousValue = previous,
                    Existed = true,
                    AppliedValue = value,
                }
            );
        }

        public override Task RestoreAsync(TweakContext ctx, BackupEntry entry, CancellationToken ct)
        {
            if (RestoreFails)
            {
                throw new InvalidOperationException("restore refused by the fake");
            }

            RestoreCount++;
            Current = entry.PreviousValue;

            return Task.CompletedTask;
        }

    }

    private sealed class FakeCatalog(IReadOnlyList<Tweak> tweaks) : ITweakCatalog
    {
        public IReadOnlyList<Tweak> All { get; } = tweaks;

        public Tweak? Find(string id) => All.FirstOrDefault(t => t.Id == id);
    }

    private sealed class FakeBackups : IBackupService
    {
        private readonly Dictionary<string, BackupSnapshot> _store = [];

        public IReadOnlyList<BackupSnapshot> LoadAll() => [.. _store.Values];

        public void Save(BackupSnapshot snapshot) => _store[snapshot.Id] = snapshot;

        public void Delete(string id) => _store.Remove(id);

        public BackupSnapshot? Latest() => _store.Values.FirstOrDefault(s => !s.Restored);

        public bool HasActiveChanges() => _store.Values.Any(s => !s.Restored);
    }

    private sealed class FakeRegistry : IRegistryService
    {
        public string? ReadValue(bool currentUser, string keyPath, string valueName) => null;

        public void WriteValue(
            bool currentUser,
            string keyPath,
            string valueName,
            string value,
            RegistryValueKind kind
        ) { }

        public void DeleteValue(bool currentUser, string keyPath, string valueName) { }

        public string? FindAdapterClassKey(string adapterId) => null;
    }

    private sealed class FakeProcess : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default) =>
            Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));

        public Task<ProcessResult> RunPowerShellAsync(string command, CancellationToken ct = default) =>
            Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
    }

    private sealed class FakeNetwork : INetworkService
    {
        public IReadOnlyList<NetworkAdapterInfo> GetAdapters() => [];

        public NetworkAdapterInfo? GetPreferredAdapter(string? preferredId) => null;

        public Task<IReadOnlyDictionary<string, string>> ReadStateAsync(
            NetworkAdapterInfo? adapter,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            );

        public Task<ProcessResult> SetMtuAsync(NetworkAdapterInfo adapter, int mtu, CancellationToken ct = default) =>
            Ok();

        public Task<ProcessResult> SetDnsAsync(
            NetworkAdapterInfo adapter,
            string[] servers,
            CancellationToken ct = default
        ) => Ok();

        public Task<ProcessResult> ResetDnsAsync(NetworkAdapterInfo adapter, CancellationToken ct = default) => Ok();

        public Task<ProcessResult> RestartAdapterAsync(NetworkAdapterInfo adapter, CancellationToken ct = default) =>
            Ok();

        public Task<ProcessResult> FlushDnsAsync(CancellationToken ct = default) => Ok();

        public Task<ProcessResult> ResetWinsockAsync(CancellationToken ct = default) => Ok();

        public Task<ProcessResult> ResetTcpIpAsync(CancellationToken ct = default) => Ok();

        public Task<ProcessResult> ClearArpCacheAsync(CancellationToken ct = default) => Ok();

        public Task<ProcessResult> RenewLeaseAsync(CancellationToken ct = default) => Ok();

        public Task<string> BuildReportAsync(NetworkAdapterInfo? adapter, CancellationToken ct = default) =>
            Task.FromResult(string.Empty);

        private static Task<ProcessResult> Ok() =>
            Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
    }
}
