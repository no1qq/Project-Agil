using ProjectAgil.Models;
using ProjectAgil.Services;
using Xunit;

namespace ProjectAgil.Tests;

public sealed class LatencyStatsTests
{
    [Fact]
    public void ARefusalIsNotPacketLoss()
    {
        var stats = new LatencyStats
        {
            Sent = 10,
            Answered = 6,
            Refused = 4,
            Loss = 0,
        };

        Assert.Equal(0, stats.Loss);
        Assert.NotEqual(string.Empty, stats.RefusedNote);
    }

    [Fact]
    public void AHandfulOfRefusalsIsNotWorthAWarning()
    {
        var stats = new LatencyStats
        {
            Sent = 85,
            Answered = 84,
            Refused = 1,
        };

        Assert.False(stats.RefusalIsWorthMentioning);
        Assert.Equal(string.Empty, stats.RefusedNote);
    }

    [Fact]
    public void RefusalsWorthMoreThanATwentiethOfChecksAreMentioned()
    {
        var stats = new LatencyStats
        {
            Sent = 80,
            Answered = 76,
            Refused = 4,
        };

        Assert.True(stats.RefusalIsWorthMentioning);
        Assert.NotEqual(string.Empty, stats.RefusedNote);
    }

    [Fact]
    public void ARefusalRightNowIsAlwaysMentionedHoweverRareItIs()
    {
        var stats = new LatencyStats
        {
            Sent = 200,
            Answered = 199,
            Refused = 1,
            Refusing = true,
        };

        Assert.True(stats.RefusalIsWorthMentioning);
    }

    [Fact]
    public void NoRefusalsMeansNoNoteEvenWhileRefusingIsSomehowSet()
    {
        var stats = new LatencyStats { Sent = 40, Answered = 40 };

        Assert.False(stats.RefusalIsWorthMentioning);
        Assert.Equal(string.Empty, stats.RefusedNote);
    }

    [Fact]
    public void AServerThatAnsweredNothingScoresZero()
    {
        var stats = new LatencyStats
        {
            Sent = 8,
            Answered = 0,
            Refused = 8,
            Average = 0,
        };

        Assert.Equal(0, stats.Grade);
        Assert.Equal("not answering", stats.GradeLabel);
    }

    [Fact]
    public void ASteadyLongDistanceLinkIsStillCalledGood()
    {
        var stats = new LatencyStats
        {
            Sent = 60,
            Answered = 60,
            Average = 120,
            Jitter = 1.5,
            Loss = 0,
        };

        Assert.True(stats.Grade >= 70, $"120 ms steady scored {stats.Grade}, which reads as a broken connection");
    }

    [Fact]
    public void JitterAndLossHurtMoreThanDistance()
    {
        var far = new LatencyStats
        {
            Sent = 60,
            Answered = 60,
            Average = 140,
            Jitter = 1,
            Loss = 0,
        };

        var close = new LatencyStats
        {
            Sent = 60,
            Answered = 60,
            Average = 25,
            Jitter = 12,
            Loss = 4,
        };

        Assert.True(far.Grade > close.Grade, $"far scored {far.Grade}, jittery close scored {close.Grade}");
    }

    [Fact]
    public void NoSamplesMeansDashesRatherThanZeroes()
    {
        var stats = new LatencyStats();

        Assert.Equal("-", stats.AverageDisplay);
        Assert.Equal("-", stats.JitterDisplay);
        Assert.Equal("-", stats.LossDisplay);
        Assert.Equal("-", stats.RangeDisplay);
    }

    [Fact]
    public void ARefusedProbeReadsAsRefusedRatherThanTimeout()
    {
        var stats = new LatencyStats { Sent = 1, Refusing = true };

        Assert.Equal("refused", stats.CurrentDisplay);
    }

    [Fact]
    public void MinecraftCapturesAreSpacedFarEnoughApartToAvoidARateLimit()
    {
        var target = new PingTarget { Host = "mc.hypixel.net", Kind = PingKind.Minecraft };
        var seconds = LatencyMonitor.EstimateCaptureSeconds(target, 20, 500);

        Assert.True(seconds >= 60, $"20 Minecraft pings estimated at {seconds}s, which is faster than the 3s floor");
    }

    [Fact]
    public void PlainPingCapturesAreNotSlowedDownByTheMinecraftFloor()
    {
        var target = new PingTarget { Host = "1.1.1.1", Kind = PingKind.Icmp };

        Assert.True(LatencyMonitor.EstimateCaptureSeconds(target, 20, 500) <= 15);
    }
}
