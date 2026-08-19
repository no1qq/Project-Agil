using ProjectAgil.ViewModels;
using Xunit;

namespace ProjectAgil.Tests;

public sealed class NavigationTests
{
    private static IReadOnlyList<string> Labels(bool advanced) =>
        [.. MainWindowViewModel.VisibleNavigation(advanced).Select(d => d.Label)];

    [Fact]
    public void SimpleModeHidesExactlyTheTwoAdvancedPages()
    {
        var hidden = Labels(true).Except(Labels(false)).ToList();

        Assert.Equal(["All settings", "Network cards"], hidden);
    }

    [Fact]
    public void TheSafetyNetIsNeverHidden() => Assert.Contains("Undo points", Labels(false));

    [Fact]
    public void SimpleModeKeepsTheOrderAdvancedModeUses()
    {
        var advanced = Labels(true);
        var simple = Labels(false);

        Assert.Equal([.. advanced.Where(simple.Contains)], simple);
    }

    [Fact]
    public void NoPageAppearsTwiceInEitherMode()
    {
        foreach (var advanced in new[] { true, false })
        {
            var pages = MainWindowViewModel.VisibleNavigation(advanced).Select(d => d.PageType).ToList();

            Assert.Equal(pages.Count, pages.Distinct().Count());
        }
    }

    [Fact]
    public void TogglingBackAndForthAlwaysGivesTheSameTwoLists()
    {
        var simple = Labels(false);
        var advanced = Labels(true);

        for (var round = 0; round < 5; round++)
        {
            Assert.Equal(simple, Labels(false));
            Assert.Equal(advanced, Labels(true));
        }
    }

    [Fact]
    public void EachCallHandsBackItsOwnListRatherThanASharedOne()
    {
        var first = MainWindowViewModel.VisibleNavigation(true);
        var second = MainWindowViewModel.VisibleNavigation(true);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void EveryNavigationEntryPointsAtARealPage()
    {
        foreach (var definition in MainWindowViewModel.VisibleNavigation(true))
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Label));
            Assert.True(
                definition.PageType.Namespace?.StartsWith("ProjectAgil.Views.Pages", StringComparison.Ordinal) == true,
                $"{definition.Label} points at {definition.PageType.FullName}"
            );
        }
    }

    [Fact]
    public void EveryNavigationIconRendersInsideTheBasicPlane()
    {
        foreach (var definition in MainWindowViewModel.VisibleNavigation(true))
        {
            Assert.True((int)definition.Icon <= 0xFFFF, $"{definition.Label} uses {definition.Icon}");
        }
    }
}
