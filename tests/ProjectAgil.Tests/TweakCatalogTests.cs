using ProjectAgil.Models;
using ProjectAgil.Services;
using Xunit;

namespace ProjectAgil.Tests;

public sealed class TweakCatalogTests
{
    private static readonly TweakCatalog Catalog = new();

    [Fact]
    public void EveryIdIsUnique()
    {
        var duplicates = Catalog
            .All.GroupBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void EveryTweakIsFindableById()
    {
        foreach (var tweak in Catalog.All)
        {
            Assert.Same(tweak, Catalog.Find(tweak.Id));
        }
    }

    [Fact]
    public void EveryTweakHasTextTheUserCanRead()
    {
        foreach (var tweak in Catalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(tweak.Name), $"{tweak.Id} has no name");
            Assert.False(string.IsNullOrWhiteSpace(tweak.Description), $"{tweak.Id} has no description");
            Assert.False(string.IsNullOrWhiteSpace(tweak.OptimizedValue), $"{tweak.Id} has no target value");
        }
    }

    [Fact]
    public void EveryPowerShellCommandFormatsWithoutThrowing()
    {
        foreach (var tweak in Catalog.All.OfType<PowerShellTweak>())
        {
            var formatted = string.Format(
                CultureInfo.InvariantCulture,
                tweak.ApplyCommand,
                tweak.OptimizedValue,
                "Ethernet"
            );

            Assert.False(string.IsNullOrWhiteSpace(formatted), $"{tweak.Id} formatted to nothing");
            Assert.DoesNotContain("{0}", formatted, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryPowerShellTweakReadsAStateKeyThatTheBatchedReadProvides()
    {
        var prefixes = new[] { "tcp.", "offload.", "nic.", "adapter." };

        foreach (var tweak in Catalog.All.OfType<PowerShellTweak>())
        {
            Assert.True(
                prefixes.Any(p => tweak.StateKey.StartsWith(p, StringComparison.OrdinalIgnoreCase)),
                $"{tweak.Id} reads {tweak.StateKey}, which NetworkService.ReadStateAsync never fills in"
            );
        }
    }

    [Fact]
    public void AdapterScopedTweaksSayTheyNeedAnAdapter()
    {
        foreach (var tweak in Catalog.All.OfType<AdapterPropertyTweak>())
        {
            Assert.True(tweak.NeedsAdapter, $"{tweak.Id} is a card property but does not require an adapter");
        }

        foreach (var tweak in Catalog.All.OfType<RegistryTweak>())
        {
            if (tweak.Scope is RegistryScope.Interface or RegistryScope.AdapterClass)
            {
                Assert.True(tweak.NeedsAdapter, $"{tweak.Id} is per adapter but does not require one");
            }
        }
    }

    [Fact]
    public void NoUserFacingStringCarriesAFancyDash()
    {
        char[] banned = ['\u2014', '\u2013', '\u2012', '\u2015', '\u2212'];

        foreach (var tweak in Catalog.All)
        {
            Assert.True(tweak.Name.IndexOfAny(banned) < 0, $"{tweak.Id} name has a fancy dash");
            Assert.True(tweak.Description.IndexOfAny(banned) < 0, $"{tweak.Id} description has a fancy dash");
            Assert.True(tweak.Impact.IndexOfAny(banned) < 0, $"{tweak.Id} impact has a fancy dash");
        }
    }
}
