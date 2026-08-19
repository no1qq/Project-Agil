using System.Text.Json.Serialization;

namespace ProjectAgil.Models;

public enum BenchmarkVerdict
{
    Inconclusive,
    NoChange,
    Better,
    Worse,
}

public sealed class BenchmarkRun
{
    public string Host { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public PingKind Kind { get; set; }

    public DateTime TakenUtc { get; set; } = DateTime.UtcNow;

    public List<double> Samples { get; set; } = [];

    [JsonIgnore]
    public IReadOnlyList<double> Answered => [.. Samples.Where(s => s >= 0)];

    [JsonIgnore]
    public int Sent => Samples.Count;

    [JsonIgnore]
    public int Refused => Samples.Count(s => s <= -2d);

    [JsonIgnore]
    public int Lost => Samples.Count(s => s is < 0 and > -2d);

    [JsonIgnore]
    public int AnsweredCount => Answered.Count;

    [JsonIgnore]
    public double Median => Statistics.Median(Answered);

    [JsonIgnore]
    public double Average => AnsweredCount == 0 ? 0 : Answered.Average();

    [JsonIgnore]
    public double Minimum => AnsweredCount == 0 ? 0 : Answered.Min();

    [JsonIgnore]
    public double Maximum => AnsweredCount == 0 ? 0 : Answered.Max();

    [JsonIgnore]
    public double P95 => Statistics.Percentile(Answered, 0.95);

    [JsonIgnore]
    public double Jitter => Statistics.MeanDeviation(Answered);

    [JsonIgnore]
    public double Loss
    {
        get
        {
            var answerable = Sent - Refused;
            return answerable <= 0 ? 0 : Lost * 100d / answerable;
        }
    }

    [JsonIgnore]
    public double StandardError => Statistics.MedianStandardError(Answered);

    [JsonIgnore]
    public string MedianDisplay => AnsweredCount == 0 ? "-" : $"{Median:0.0} ms";

    [JsonIgnore]
    public string P95Display => AnsweredCount == 0 ? "-" : $"{P95:0.0} ms";

    [JsonIgnore]
    public string JitterDisplay => AnsweredCount == 0 ? "-" : $"{Jitter:0.0} ms";

    [JsonIgnore]
    public string LossDisplay => Sent == 0 ? "-" : $"{Loss:0.#} %";

    [JsonIgnore]
    public string CountDisplay => $"{AnsweredCount} of {Sent} answered";
}

public sealed class BenchmarkComparison
{
    public BenchmarkRun? Before { get; set; }

    public BenchmarkRun? After { get; set; }

    public bool IncompleteBecauseRestartPending { get; set; }

    [JsonIgnore]
    public bool HasBoth => Before is not null && After is not null;

    [JsonIgnore]
    public double MedianDelta => HasBoth ? After!.Median - Before!.Median : 0;

    [JsonIgnore]
    public double JitterDelta => HasBoth ? After!.Jitter - Before!.Jitter : 0;

    [JsonIgnore]
    public double LossDelta => HasBoth ? After!.Loss - Before!.Loss : 0;

    [JsonIgnore]
    public double P95Delta => HasBoth ? After!.P95 - Before!.P95 : 0;

    [JsonIgnore]
    public double NoiseFloor =>
        !HasBoth
            ? 0
            : Math.Max(
                MinimumMeaningfulMs,
                2 * Math.Sqrt((Before!.StandardError * Before.StandardError) + (After!.StandardError * After.StandardError))
            );

    [JsonIgnore]
    public BenchmarkVerdict Verdict
    {
        get
        {
            if (!HasBoth || Before!.AnsweredCount < MinimumSamples || After!.AnsweredCount < MinimumSamples)
            {
                return BenchmarkVerdict.Inconclusive;
            }

            if (Math.Abs(MedianDelta) <= NoiseFloor)
            {
                return BenchmarkVerdict.NoChange;
            }

            return MedianDelta < 0 ? BenchmarkVerdict.Better : BenchmarkVerdict.Worse;
        }
    }

    [JsonIgnore]
    public string VerdictLabel =>
        Verdict switch
        {
            BenchmarkVerdict.Better => "Ping improved",
            BenchmarkVerdict.Worse => "Ping got worse",
            BenchmarkVerdict.NoChange => "No measurable change",
            _ => "Not enough data",
        };

    [JsonIgnore]
    public string VerdictDetail =>
        Verdict switch
        {
            BenchmarkVerdict.Better or BenchmarkVerdict.Worse =>
                $"Typical ping moved {DeltaDisplay(MedianDelta)}, which is more than the {NoiseFloor:0.0} ms of noise in the measurement.",
            BenchmarkVerdict.NoChange =>
                $"Typical ping moved {DeltaDisplay(MedianDelta)}, which is inside the {NoiseFloor:0.0} ms of noise in the measurement, so it cannot be called a real change.",
            _ =>
                $"Only {Before?.AnsweredCount ?? 0} and {After?.AnsweredCount ?? 0} replies came back. At least {MinimumSamples} on each side are needed before a comparison means anything.",
        };

    [JsonIgnore]
    public string MedianDeltaDisplay => DeltaDisplay(MedianDelta);

    [JsonIgnore]
    public string JitterDeltaDisplay => DeltaDisplay(JitterDelta);

    [JsonIgnore]
    public string P95DeltaDisplay => DeltaDisplay(P95Delta);

    [JsonIgnore]
    public string LossDeltaDisplay =>
        !HasBoth ? "-"
        : Math.Abs(LossDelta) < 0.05 ? "unchanged"
        : $"{(LossDelta < 0 ? "-" : "+")}{Math.Abs(LossDelta):0.#} %";

    [JsonIgnore]
    public string SummaryLine =>
        !HasBoth
            ? string.Empty
            : $"{Before!.MedianDisplay} to {After!.MedianDisplay} typical, jitter {Before.JitterDisplay} to {After.JitterDisplay}";

    [JsonIgnore]
    public string RestartNote =>
        IncompleteBecauseRestartPending
            ? "Some of the changes only take effect after a restart, so this measurement does not include them yet."
            : string.Empty;

    public const int MinimumSamples = 12;

    private const double MinimumMeaningfulMs = 0.5;

    private static string DeltaDisplay(double value) =>
        Math.Abs(value) < 0.05 ? "unchanged" : $"{(value < 0 ? "-" : "+")}{Math.Abs(value):0.0} ms";
}

public static class Statistics
{
    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(v => v).ToArray();
        var middle = sorted.Length / 2;

        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2d;
    }

    public static double Percentile(IReadOnlyList<double> values, double fraction)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(v => v).ToArray();
        var rank = fraction * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);

        return lower == upper ? sorted[lower] : sorted[lower] + ((sorted[upper] - sorted[lower]) * (rank - lower));
    }

    public static double StandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var mean = values.Average();
        var sum = values.Sum(v => (v - mean) * (v - mean));

        return Math.Sqrt(sum / (values.Count - 1));
    }

    public static double MedianStandardError(IReadOnlyList<double> values) =>
        values.Count < 2 ? 0 : 1.2533 * StandardDeviation(values) / Math.Sqrt(values.Count);

    public static double MeanDeviation(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var total = 0d;
        for (var i = 1; i < values.Count; i++)
        {
            total += Math.Abs(values[i] - values[i - 1]);
        }

        return total / (values.Count - 1);
    }
}
