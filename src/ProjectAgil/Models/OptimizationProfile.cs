namespace ProjectAgil.Models;

public sealed class OptimizationProfile
{
    public string Name { get; set; } = "Default";

    public bool SmartPackets { get; set; } = true;

    public int Latency { get; set; } = 75;

    public int Responsiveness { get; set; } = 60;

    public TuningLevel Tuning { get; set; } = TuningLevel.Restricted;

    public ConnectionType Connection { get; set; } = ConnectionType.Fiber;

    public bool StableConnection { get; set; } = true;

    public bool IncludeAdvanced { get; set; }

    public List<string> ExcludedTweaks { get; set; } = [];

    public string? AdapterId { get; set; }

    public DateTime SavedUtc { get; set; } = DateTime.UtcNow;

    public OptimizationProfile Clone() =>
        new()
        {
            Name = Name,
            SmartPackets = SmartPackets,
            Latency = Latency,
            Responsiveness = Responsiveness,
            Tuning = Tuning,
            Connection = Connection,
            StableConnection = StableConnection,
            IncludeAdvanced = IncludeAdvanced,
            ExcludedTweaks = [.. ExcludedTweaks],
            AdapterId = AdapterId,
            SavedUtc = SavedUtc,
        };

    public string SavedDisplay => SavedUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    public string Summary =>
        $"{Connection.ToDisplay()}  -  tuning {Tuning.ToDisplay()}  -  latency {Latency}  -  responsiveness {Responsiveness}";
}

public sealed class PlanItem
{
    public required Tweak Tweak { get; init; }

    public required string DesiredValue { get; init; }

    public required string Reason { get; init; }

    public TweakStatus Status { get; set; } = TweakStatus.Unknown;

    public string? CurrentValue { get; set; }

    public bool Selected { get; set; } = true;

    public string Name => Tweak.Name;

    public string Description => Tweak.Description;

    public string CategoryDisplay => Tweak.Category.ToDisplay();

    public string RiskDisplay => Tweak.Risk.ToDisplay();

    public string CurrentDisplay => CurrentValue ?? "not set";
}
