namespace ProjectAgil.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";

    public string AccentColor { get; set; } = "#00C8FF";

    public bool StartWithWindows { get; set; }

    public bool StartMinimized { get; set; }

    public bool MinimizeToTray { get; set; } = true;

    public bool CloseToTray { get; set; } = true;

    public bool ConfirmBeforeApply { get; set; } = true;

    public int PingIntervalMs { get; set; } = 500;

    public int PingTimeoutMs { get; set; } = 1000;

    public int HistorySize { get; set; } = 240;

    public string? PreferredAdapterId { get; set; }

    public bool AutoApplyOnLaunch { get; set; }

    public bool AutoSaveProfile { get; set; } = true;

    public int SchemaVersion { get; set; }

    public List<PingTarget> Targets { get; set; } =
    [
        new()
        {
            Name = "Hypixel",
            Host = "mc.hypixel.net",
            Kind = PingKind.Minecraft,
            IsPrimary = true,
        },
        new() { Name = "Cloudflare", Host = "1.1.1.1" },
        new() { Name = "Google DNS", Host = "8.8.8.8" },
    ];
}

public enum PingKind
{
    Icmp,
    Minecraft,
}

public sealed class PingTarget
{
    public string Name { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public PingKind Kind { get; set; } = PingKind.Icmp;

    public int Port { get; set; } = 25565;

    public bool IsPrimary { get; set; }

    public bool Enabled { get; set; } = true;

    public string KindDisplay => Kind == PingKind.Minecraft ? "Minecraft" : "Ping";
}
