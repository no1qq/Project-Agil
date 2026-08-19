using ProjectAgil.Models;
using Xunit;

namespace ProjectAgil.Tests;

public sealed class StatisticsTests
{
    [Fact]
    public void MedianOfAnOddCountIsTheMiddleValue() =>
        Assert.Equal(30, Statistics.Median([50, 10, 30, 40, 20]));

    [Fact]
    public void MedianOfAnEvenCountIsTheMeanOfTheTwoMiddleValues() =>
        Assert.Equal(25, Statistics.Median([10, 20, 30, 40]));

    [Fact]
    public void MedianOfNothingIsZero() => Assert.Equal(0, Statistics.Median([]));

    [Fact]
    public void MedianIgnoresTheOrderItIsGivenIn() =>
        Assert.Equal(Statistics.Median([1, 2, 3, 4, 5]), Statistics.Median([5, 3, 1, 4, 2]));

    [Fact]
    public void ASingleHugeSpikeMovesTheMeanButNotTheMedian()
    {
        double[] samples = [20, 20, 20, 20, 5000];

        Assert.Equal(20, Statistics.Median(samples));
        Assert.True(samples.Average() > 1000);
    }

    [Fact]
    public void PercentileInterpolatesBetweenNeighbours() =>
        Assert.Equal(19.5, Statistics.Percentile([10, 12, 14, 16, 18, 20], 0.95), 1);

    [Fact]
    public void StandardDeviationOfIdenticalValuesIsZero() =>
        Assert.Equal(0, Statistics.StandardDeviation([7, 7, 7, 7]));
}

public sealed class BenchmarkComparisonTests
{
    private static BenchmarkRun Run(params double[] samples) => new() { Samples = [.. samples] };

    private static BenchmarkRun Steady(double value, int count) =>
        new() { Samples = [.. Enumerable.Repeat(value, count)] };

    [Fact]
    public void TooFewRepliesIsInconclusive()
    {
        var comparison = new BenchmarkComparison { Before = Run(20, 21, 22), After = Run(10, 11, 12) };

        Assert.Equal(BenchmarkVerdict.Inconclusive, comparison.Verdict);
    }

    [Fact]
    public void AMissingSideIsInconclusive() =>
        Assert.Equal(BenchmarkVerdict.Inconclusive, new BenchmarkComparison { Before = Steady(20, 30) }.Verdict);

    [Fact]
    public void AnIdenticalDistributionIsNotCalledAnImprovement()
    {
        var comparison = new BenchmarkComparison { Before = Steady(40, 30), After = Steady(40, 30) };

        Assert.Equal(BenchmarkVerdict.NoChange, comparison.Verdict);
    }

    [Fact]
    public void ALargeDropIsCalledBetter()
    {
        var comparison = new BenchmarkComparison { Before = Steady(80, 30), After = Steady(40, 30) };

        Assert.Equal(BenchmarkVerdict.Better, comparison.Verdict);
        Assert.True(comparison.MedianDelta < 0);
    }

    [Fact]
    public void ALargeRiseIsCalledWorse() =>
        Assert.Equal(
            BenchmarkVerdict.Worse,
            new BenchmarkComparison { Before = Steady(40, 30), After = Steady(90, 30) }.Verdict
        );

    [Fact]
    public void ATinyShiftInsideNoisyDataIsNotCalledAnImprovement()
    {
        var before = new BenchmarkRun();
        var after = new BenchmarkRun();

        for (var i = 0; i < 40; i++)
        {
            before.Samples.Add(40 + (i % 2 == 0 ? 25 : -25));
            after.Samples.Add(39 + (i % 2 == 0 ? 25 : -25));
        }

        var comparison = new BenchmarkComparison { Before = before, After = after };

        Assert.Equal(BenchmarkVerdict.NoChange, comparison.Verdict);
    }

    [Fact]
    public void TheSameShiftInSteadyDataIsCalledAnImprovement()
    {
        var before = new BenchmarkRun();
        var after = new BenchmarkRun();

        for (var i = 0; i < 40; i++)
        {
            before.Samples.Add(40 + (i % 2 == 0 ? 0.2 : -0.2));
            after.Samples.Add(37 + (i % 2 == 0 ? 0.2 : -0.2));
        }

        var comparison = new BenchmarkComparison { Before = before, After = after };

        Assert.Equal(BenchmarkVerdict.Better, comparison.Verdict);
    }

    [Fact]
    public void RefusedProbesAreNotCountedAsLoss()
    {
        var run = new BenchmarkRun { Samples = [20, 20, -2, -2, 20] };

        Assert.Equal(2, run.Refused);
        Assert.Equal(0, run.Lost);
        Assert.Equal(0, run.Loss);
    }

    [Fact]
    public void TimeoutsAreCountedAsLoss()
    {
        var run = new BenchmarkRun { Samples = [20, 20, -1, 20] };

        Assert.Equal(1, run.Lost);
        Assert.Equal(0, run.Refused);
        Assert.Equal(25, run.Loss);
    }

    [Fact]
    public void ARunWhereEverythingWasRefusedReportsNoLossAndNoAnswers()
    {
        var run = new BenchmarkRun { Samples = [-2, -2, -2] };

        Assert.Equal(0, run.AnsweredCount);
        Assert.Equal(0, run.Loss);
    }

    [Fact]
    public void TheRestartNoteOnlyAppearsWhenSomethingIsWaitingOnARestart()
    {
        var without = new BenchmarkComparison { Before = Steady(40, 30), After = Steady(40, 30) };
        var with = new BenchmarkComparison
        {
            Before = Steady(40, 30),
            After = Steady(40, 30),
            IncompleteBecauseRestartPending = true,
        };

        Assert.Equal(string.Empty, without.RestartNote);
        Assert.NotEqual(string.Empty, with.RestartNote);
    }
}
