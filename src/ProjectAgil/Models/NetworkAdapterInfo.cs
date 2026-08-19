namespace ProjectAgil.Models;

public sealed class NetworkAdapterInfo
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string InterfaceType { get; init; } = string.Empty;

    public bool IsUp { get; init; }

    public bool IsWireless { get; init; }

    public long SpeedBitsPerSecond { get; init; }

    public string MacAddress { get; init; } = string.Empty;

    public string IPv4 { get; init; } = string.Empty;

    public string Gateway { get; init; } = string.Empty;

    public string DnsServers { get; init; } = string.Empty;

    public int Mtu { get; init; }

    public bool IsDefaultRoute { get; init; }

    public string SpeedDisplay =>
        SpeedBitsPerSecond <= 0
            ? "unknown"
            : SpeedBitsPerSecond >= 1_000_000_000
                ? $"{SpeedBitsPerSecond / 1_000_000_000d:0.#} Gbps"
                : $"{SpeedBitsPerSecond / 1_000_000d:0.#} Mbps";

    public string StatusDisplay => IsUp ? "Connected" : "Down";

    public string Summary => string.IsNullOrWhiteSpace(IPv4) ? Name : $"{Name}  -  {IPv4}";

    public override string ToString() => Name;
}
