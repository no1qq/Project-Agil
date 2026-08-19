using Microsoft.Win32;
using ProjectAgil.Models;
using ProjectAgil.Services;
using Xunit;

namespace ProjectAgil.Tests;

public sealed class PlanBuilderTests
{
    private static readonly TweakCatalog Catalog = new();

    private static readonly NetworkAdapterInfo Adapter = new()
    {
        Id = "{11111111-2222-3333-4444-555555555555}",
        Name = "Ethernet",
        Description = "Test card",
        IsUp = true,
        IsDefaultRoute = true,
    };

    private static TweakContext Context(NetworkAdapterInfo? adapter) =>
        new()
        {
            Registry = new FakeRegistry(),
            Process = new FakeProcess(),
            Adapter = adapter,
            State = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };

    private static IReadOnlyList<PlanItem> Build(OptimizationProfile profile, NetworkAdapterInfo? adapter = null) =>
        new PlanBuilder(Catalog).Build(profile, Context(adapter ?? Adapter));

    [Fact]
    public void APlanNeverContainsTheSameTweakTwice()
    {
        var plan = Build(new OptimizationProfile { Latency = 100, Responsiveness = 100, IncludeAdvanced = true });

        var duplicates = plan.GroupBy(p => p.Tweak.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void TurningTheSlidersUpNeverShrinksThePlan()
    {
        var quiet = Build(new OptimizationProfile { Latency = 0, Responsiveness = 0, SmartPackets = false });
        var loud = Build(new OptimizationProfile { Latency = 100, Responsiveness = 100, SmartPackets = true });

        Assert.True(loud.Count > quiet.Count, $"expected more than {quiet.Count} items, got {loud.Count}");
    }

    [Fact]
    public void AdvancedTweaksStayOutUnlessAskedFor()
    {
        var without = Build(
            new OptimizationProfile { Latency = 100, Responsiveness = 100, IncludeAdvanced = false }
        );

        Assert.DoesNotContain(without, p => p.Tweak.Risk == TweakRisk.Advanced);

        var with = Build(new OptimizationProfile { Latency = 100, Responsiveness = 100, IncludeAdvanced = true });

        Assert.Contains(with, p => p.Tweak.Risk == TweakRisk.Advanced);
    }

    [Fact]
    public void AnExcludedTweakNeverEntersThePlan()
    {
        var profile = new OptimizationProfile { Latency = 100, Responsiveness = 100 };
        var before = Build(profile);
        var victim = before[0].Tweak.Id;

        profile.ExcludedTweaks.Add(victim);

        Assert.DoesNotContain(Build(profile), p => p.Tweak.Id.Equals(victim, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CardSpecificTweaksAreLeftOutWhenThereIsNoCard()
    {
        var plan = new PlanBuilder(Catalog).Build(
            new OptimizationProfile { Latency = 100, Responsiveness = 100, IncludeAdvanced = true },
            Context(null)
        );

        Assert.DoesNotContain(plan, p => p.Tweak.NeedsAdapter);
    }

    [Theory]
    [InlineData(ConnectionType.Fiber, "1500")]
    [InlineData(ConnectionType.Cable, "1500")]
    [InlineData(ConnectionType.Dsl, "1492")]
    [InlineData(ConnectionType.Mobile, "1428")]
    public void TheInternetTypeDecidesTheMtu(ConnectionType connection, string expected)
    {
        var plan = Build(new OptimizationProfile { Connection = connection });
        var mtu = plan.Single(p => p.Tweak.Id == "iface.mtu");

        Assert.Equal(expected, mtu.DesiredValue);
    }

    [Fact]
    public void AnUnstableLinkKeepsTheRecoverySettingsOn()
    {
        var stable = Build(new OptimizationProfile { Latency = 100, StableConnection = true });
        var shaky = Build(new OptimizationProfile { Latency = 100, StableConnection = false });

        Assert.Equal("Disabled", stable.Single(p => p.Tweak.Id == "tcp.nonsack").DesiredValue);
        Assert.Equal("Enabled", shaky.Single(p => p.Tweak.Id == "tcp.nonsack").DesiredValue);
    }

    [Fact]
    public void EveryPlannedItemCarriesAReasonTheUserCanRead()
    {
        foreach (var item in Build(new OptimizationProfile { Latency = 100, Responsiveness = 100 }))
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Reason), $"{item.Tweak.Id} has no reason");
        }
    }

    private sealed class FakeRegistry : IRegistryService
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public string? ReadValue(bool currentUser, string keyPath, string valueName) =>
            _values.TryGetValue($"{keyPath}|{valueName}", out var value) ? value : null;

        public void WriteValue(
            bool currentUser,
            string keyPath,
            string valueName,
            string value,
            RegistryValueKind kind
        ) => _values[$"{keyPath}|{valueName}"] = value;

        public void DeleteValue(bool currentUser, string keyPath, string valueName) =>
            _values.Remove($"{keyPath}|{valueName}");

        public string? FindAdapterClassKey(string adapterId) => @"SYSTEM\Fake\Class\0001";
    }

    private sealed class FakeProcess : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default) =>
            Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));

        public Task<ProcessResult> RunPowerShellAsync(string command, CancellationToken ct = default) =>
            Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
    }
}
