using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using ProjectAgil.Models;

namespace ProjectAgil.Services;

public interface INetworkService
{
    IReadOnlyList<NetworkAdapterInfo> GetAdapters();

    NetworkAdapterInfo? GetPreferredAdapter(string? preferredId);

    Task<IReadOnlyDictionary<string, string>> ReadStateAsync(NetworkAdapterInfo? adapter, CancellationToken ct = default);

    Task<ProcessResult> SetMtuAsync(NetworkAdapterInfo adapter, int mtu, CancellationToken ct = default);

    Task<ProcessResult> SetDnsAsync(NetworkAdapterInfo adapter, string[] servers, CancellationToken ct = default);

    Task<ProcessResult> ResetDnsAsync(NetworkAdapterInfo adapter, CancellationToken ct = default);

    Task<ProcessResult> RestartAdapterAsync(NetworkAdapterInfo adapter, CancellationToken ct = default);

    Task<ProcessResult> FlushDnsAsync(CancellationToken ct = default);

    Task<ProcessResult> ResetWinsockAsync(CancellationToken ct = default);

    Task<ProcessResult> ResetTcpIpAsync(CancellationToken ct = default);

    Task<ProcessResult> ClearArpCacheAsync(CancellationToken ct = default);

    Task<ProcessResult> RenewLeaseAsync(CancellationToken ct = default);

    Task<string> BuildReportAsync(NetworkAdapterInfo? adapter, CancellationToken ct = default);
}

public sealed class NetworkService(IProcessRunner process) : INetworkService
{
    private static readonly string[] TcpProperties =
    [
        "AutoTuningLevelLocal",
        "CongestionProvider",
        "EcnCapability",
        "Timestamps",
        "ScalingHeuristics",
        "NonSackRttResiliency",
        "MinRto",
        "InitialRto",
        "MaxSynRetransmissions",
        "InitialCongestionWindow",
        "DelayedAckFrequency",
        "DelayedAckTimeout",
        "CwndRestart",
        "ForceWS",
        "MemoryPressureProtection",
        "AutomaticUseCustom",
    ];

    private static readonly string[] OffloadProperties =
    [
        "ReceiveSideScaling",
        "ReceiveSegmentCoalescing",
        "Chimney",
        "TaskOffload",
        "PacketCoalescingFilter",
        "NetworkDirect",
    ];

    public IReadOnlyList<NetworkAdapterInfo> GetAdapters()
    {
        var list = new List<NetworkAdapterInfo>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            IPInterfaceProperties? properties = null;
            try
            {
                properties = nic.GetIPProperties();
            }
            catch
            {
            }

            var gateway = properties
                ?.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.Address.ToString();

            var ipv4 = properties
                ?.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.Address.ToString();

            var dns = properties
                ?.DnsAddresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .ToArray() ?? [];

            var mtu = 0;
            try
            {
                mtu = properties?.GetIPv4Properties()?.Mtu ?? 0;
            }
            catch
            {
            }

            list.Add(
                new NetworkAdapterInfo
                {
                    Id = nic.Id,
                    Name = nic.Name,
                    Description = nic.Description,
                    InterfaceType = nic.NetworkInterfaceType.ToString(),
                    IsUp = nic.OperationalStatus == OperationalStatus.Up,
                    IsWireless = nic.NetworkInterfaceType is NetworkInterfaceType.Wireless80211,
                    SpeedBitsPerSecond = nic.Speed,
                    MacAddress = FormatMac(nic.GetPhysicalAddress()),
                    IPv4 = ipv4 ?? string.Empty,
                    Gateway = gateway ?? string.Empty,
                    DnsServers = string.Join(", ", dns),
                    Mtu = mtu,
                    IsDefaultRoute = !string.IsNullOrEmpty(gateway),
                }
            );
        }

        return
        [
            .. list.OrderByDescending(a => a.IsDefaultRoute)
                .ThenByDescending(a => a.IsUp)
                .ThenByDescending(a => a.SpeedBitsPerSecond),
        ];
    }

    public NetworkAdapterInfo? GetPreferredAdapter(string? preferredId)
    {
        var adapters = GetAdapters();

        if (!string.IsNullOrWhiteSpace(preferredId))
        {
            var match = adapters.FirstOrDefault(a => a.Id == preferredId);
            if (match is not null)
            {
                return match;
            }
        }

        return adapters.FirstOrDefault(a => a.IsUp && a.IsDefaultRoute) ?? adapters.FirstOrDefault(a => a.IsUp);
    }

    public async Task<IReadOnlyDictionary<string, string>> ReadStateAsync(
        NetworkAdapterInfo? adapter,
        CancellationToken ct = default
    )
    {
        var script = BuildStateScript(adapter?.Name);
        var result = await process.RunPowerShellAsync(script, ct).ConfigureAwait(false);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var json = result.StdOut?.Trim();

        if (string.IsNullOrWhiteSpace(json))
        {
            return map;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.ToString(),
                    JsonValueKind.True => "True",
                    JsonValueKind.False => "False",
                    _ => null,
                };

                if (!string.IsNullOrWhiteSpace(value))
                {
                    map[property.Name] = value!;
                }
            }
        }
        catch (JsonException)
        {
        }

        return map;
    }

    private static string BuildStateScript(string? adapterName)
    {
        var tcp = string.Join(",", TcpProperties.Select(p => $"'{p}'"));
        var offload = string.Join(",", OffloadProperties.Select(p => $"'{p}'"));
        var adapterLiteral = adapterName is null ? "$null" : $"'{adapterName.Replace("'", "''")}'";

        return $$"""
            $ErrorActionPreference = 'SilentlyContinue'
            $r = [ordered]@{}
            $t = Get-NetTCPSetting -SettingName Internet
            if ($t) {
                foreach ($p in @({{tcp}})) { $v = $t.$p; if ($null -ne $v) { $r["tcp.$p"] = "$v" } }
            }
            $o = Get-NetOffloadGlobalSetting
            if ($o) {
                foreach ($p in @({{offload}})) { $v = $o.$p; if ($null -ne $v) { $r["offload.$p"] = "$v" } }
            }
            $name = {{adapterLiteral}}
            if ($name) {
                foreach ($ap in @(Get-NetAdapterAdvancedProperty -Name $name)) {
                    if ($ap.RegistryKeyword) { $r["nic." + $ap.RegistryKeyword] = ($ap.RegistryValue -join ',') }
                }
                $ad = Get-NetAdapter -Name $name
                if ($ad) {
                    $r["adapter.Mtu"] = "$($ad.MtuSize)"
                    $r["adapter.LinkSpeed"] = "$($ad.LinkSpeed)"
                    $r["adapter.ifIndex"] = "$($ad.ifIndex)"
                }
            }
            $r | ConvertTo-Json -Compress -Depth 3
            """;
    }

    public Task<ProcessResult> SetMtuAsync(NetworkAdapterInfo adapter, int mtu, CancellationToken ct = default) =>
        process.RunAsync(
            "netsh.exe",
            $"interface ipv4 set subinterface \"{adapter.Name}\" mtu={mtu} store=persistent",
            ct
        );

    public Task<ProcessResult> SetDnsAsync(NetworkAdapterInfo adapter, string[] servers, CancellationToken ct = default)
    {
        var list = string.Join(",", servers.Select(s => $"'{s}'"));
        return process.RunPowerShellAsync(
            $"Set-DnsClientServerAddress -InterfaceAlias '{Escape(adapter.Name)}' -ServerAddresses ({list}) -ErrorAction Stop",
            ct
        );
    }

    public Task<ProcessResult> ResetDnsAsync(NetworkAdapterInfo adapter, CancellationToken ct = default) =>
        process.RunPowerShellAsync(
            $"Set-DnsClientServerAddress -InterfaceAlias '{Escape(adapter.Name)}' -ResetServerAddresses -ErrorAction Stop",
            ct
        );

    public Task<ProcessResult> RestartAdapterAsync(NetworkAdapterInfo adapter, CancellationToken ct = default) =>
        process.RunPowerShellAsync(
            $"Restart-NetAdapter -Name '{Escape(adapter.Name)}' -Confirm:$false -ErrorAction Stop",
            ct
        );

    public Task<ProcessResult> FlushDnsAsync(CancellationToken ct = default) =>
        process.RunAsync("ipconfig.exe", "/flushdns", ct);

    public Task<ProcessResult> ResetWinsockAsync(CancellationToken ct = default) =>
        process.RunAsync("netsh.exe", "winsock reset", ct);

    public Task<ProcessResult> ResetTcpIpAsync(CancellationToken ct = default) =>
        process.RunAsync("netsh.exe", "int ip reset", ct);

    public Task<ProcessResult> ClearArpCacheAsync(CancellationToken ct = default) =>
        process.RunAsync("netsh.exe", "interface ip delete arpcache", ct);

    public async Task<ProcessResult> RenewLeaseAsync(CancellationToken ct = default)
    {
        _ = await process.RunAsync("ipconfig.exe", "/release", ct).ConfigureAwait(false);
        return await process.RunAsync("ipconfig.exe", "/renew", ct).ConfigureAwait(false);
    }

    public async Task<string> BuildReportAsync(NetworkAdapterInfo? adapter, CancellationToken ct = default)
    {
        var state = await ReadStateAsync(adapter, ct).ConfigureAwait(false);
        var builder = new System.Text.StringBuilder();

        _ = builder.AppendLine("Project-Agil network report");
        _ = builder.AppendLine($"Generated  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _ = builder.AppendLine($"Machine    {Environment.MachineName}");
        _ = builder.AppendLine($"OS         {Environment.OSVersion.VersionString}");
        _ = builder.AppendLine();

        if (adapter is not null)
        {
            _ = builder.AppendLine("[Adapter]");
            _ = builder.AppendLine($"Name        {adapter.Name}");
            _ = builder.AppendLine($"Driver      {adapter.Description}");
            _ = builder.AppendLine($"Id          {adapter.Id}");
            _ = builder.AppendLine($"Link        {adapter.SpeedDisplay}");
            _ = builder.AppendLine($"IPv4        {adapter.IPv4}");
            _ = builder.AppendLine($"Gateway     {adapter.Gateway}");
            _ = builder.AppendLine($"DNS         {adapter.DnsServers}");
            _ = builder.AppendLine($"MTU         {adapter.Mtu}");
            _ = builder.AppendLine();
        }

        _ = builder.AppendLine("[State]");
        foreach (var pair in state.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            _ = builder.AppendLine($"{pair.Key,-42}{pair.Value}");
        }

        return builder.ToString();
    }

    private static string Escape(string value) => value.Replace("'", "''");

    private static string FormatMac(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 0 ? string.Empty : string.Join(":", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
