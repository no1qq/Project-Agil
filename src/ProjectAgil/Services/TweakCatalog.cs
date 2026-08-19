using Microsoft.Win32;
using ProjectAgil.Models;

namespace ProjectAgil.Services;

public interface ITweakCatalog
{
    IReadOnlyList<Tweak> All { get; }

    Tweak? Find(string id);
}

public sealed class TweakCatalog : ITweakCatalog
{
    private const string TcpParameters = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
    private const string SystemProfile = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string GamesTask = $@"{SystemProfile}\Tasks\Games";
    private const string ImageFileOptions = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
    private const string SetTcp = "Set-NetTCPSetting -SettingName Internet";
    private const string SetOffload = "Set-NetOffloadGlobalSetting";

    private readonly Dictionary<string, Tweak> _byId;

    public TweakCatalog()
    {
        All = Build();
        _byId = All.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<Tweak> All { get; }

    public Tweak? Find(string id) => _byId.TryGetValue(id, out var tweak) ? tweak : null;

    private static List<Tweak> Build() =>
    [
        new PowerShellTweak
        {
            Id = "tcp.autotuning",
            Name = "Receive window auto-tuning",
            Description =
                "Controls how aggressively Windows grows the TCP receive window. Restricted keeps the window small so buffers stay shallow and packets are acknowledged sooner.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Safe,
            OptimizedValue = "Restricted",
            StateKey = "tcp.AutoTuningLevelLocal",
            TargetLabel = "Set-NetTCPSetting -AutoTuningLevelLocal",
            ApplyCommand = $"{SetTcp} -AutoTuningLevelLocal {{0}} -ErrorAction Stop",
            Impact = "Lower buffer bloat, steadier ping under load",
        },
        new PowerShellTweak
        {
            Id = "tcp.congestion",
            Name = "Congestion control provider",
            Description =
                "The algorithm that decides how fast the sender ramps up. CUBIC recovers quickly on clean links, NewReno backs off more gently on lossy ones.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Safe,
            OptimizedValue = "CUBIC",
            StateKey = "tcp.CongestionProvider",
            TargetLabel = "Set-NetTCPSetting -CongestionProvider",
            ApplyCommand = $"{SetTcp} -CongestionProvider {{0}} -ErrorAction Stop",
            Impact = "Faster recovery after packet loss",
        },
        new PowerShellTweak
        {
            Id = "tcp.ecn",
            Name = "Explicit congestion notification",
            Description =
                "Lets routers mark packets instead of dropping them. Many consumer routers implement it badly, which shows up as random latency spikes.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Safe,
            OptimizedValue = "Disabled",
            StateKey = "tcp.EcnCapability",
            TargetLabel = "Set-NetTCPSetting -EcnCapability",
            ApplyCommand = $"{SetTcp} -EcnCapability {{0}} -ErrorAction Stop",
            Impact = "Removes a common source of latency spikes",
        },
        new PowerShellTweak
        {
            Id = "tcp.timestamps",
            Name = "RFC 1323 timestamps",
            Description =
                "Adds 12 bytes of timestamp data to every TCP segment. Turning it off shrinks each packet slightly and cuts a little processing per segment.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Safe,
            OptimizedValue = "Disabled",
            StateKey = "tcp.Timestamps",
            TargetLabel = "Set-NetTCPSetting -Timestamps",
            ApplyCommand = $"{SetTcp} -Timestamps {{0}} -ErrorAction Stop",
            Impact = "Marginally smaller packets",
        },
        new PowerShellTweak
        {
            Id = "tcp.minrto",
            Name = "Minimum retransmission timeout",
            Description =
                "The floor on how long Windows waits before assuming a packet was lost. Lowering it means a dropped packet is resent sooner instead of stalling the stream.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Moderate,
            OptimizedValue = "300",
            StateKey = "tcp.MinRto",
            TargetLabel = "Set-NetTCPSetting -MinRtoMs",
            ApplyCommand = $"{SetTcp} -MinRtoMs {{0}} -ErrorAction Stop",
            Impact = "Shorter freeze after a lost packet",
        },
        new PowerShellTweak
        {
            Id = "tcp.initialrto",
            Name = "Initial retransmission timeout",
            Description =
                "How long the very first unanswered packet of a connection waits before being resent. Lower values make reconnects to a server feel instant.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Moderate,
            OptimizedValue = "1000",
            StateKey = "tcp.InitialRto",
            TargetLabel = "Set-NetTCPSetting -InitialRtoMs",
            ApplyCommand = $"{SetTcp} -InitialRtoMs {{0}} -ErrorAction Stop",
            Impact = "Faster initial connection to a server",
        },
        new PowerShellTweak
        {
            Id = "tcp.maxsyn",
            Name = "Max SYN retransmissions",
            Description =
                "How many times a connection attempt is retried before giving up. Fewer retries fail faster so a client can move on instead of hanging.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Moderate,
            OptimizedValue = "2",
            StateKey = "tcp.MaxSynRetransmissions",
            TargetLabel = "Set-NetTCPSetting -MaxSynRetransmissions",
            ApplyCommand = $"{SetTcp} -MaxSynRetransmissions {{0}} -ErrorAction Stop",
            Impact = "Connections fail fast instead of hanging",
        },
        new PowerShellTweak
        {
            Id = "tcp.icw",
            Name = "Initial congestion window",
            Description =
                "How many segments may be in flight before the first acknowledgement arrives. A larger window pushes the first burst of data out in one go.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Moderate,
            OptimizedValue = "10",
            StateKey = "tcp.InitialCongestionWindow",
            TargetLabel = "Set-NetTCPSetting -InitialCongestionWindowMss",
            ApplyCommand = $"{SetTcp} -InitialCongestionWindowMss {{0}} -ErrorAction Stop",
            Impact = "Quicker start on every new connection",
        },
        new PowerShellTweak
        {
            Id = "tcp.nonsack",
            Name = "Non-SACK RTT resiliency",
            Description =
                "A compatibility path for servers that do not support selective acknowledgement. Modern Minecraft servers all do, so the extra resiliency only adds delay.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Safe,
            OptimizedValue = "Disabled",
            StateKey = "tcp.NonSackRttResiliency",
            TargetLabel = "Set-NetTCPSetting -NonSackRttResiliency",
            ApplyCommand = $"{SetTcp} -NonSackRttResiliency {{0}} -ErrorAction Stop",
            Impact = "Removes a legacy delay path",
        },
        new PowerShellTweak
        {
            Id = "tcp.scalingheuristics",
            Name = "Window scaling heuristics",
            Description =
                "Lets Windows silently override your auto-tuning choice when it thinks the network needs it. Disabling keeps your setting exactly as configured.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Safe,
            OptimizedValue = "Disabled",
            StateKey = "tcp.ScalingHeuristics",
            TargetLabel = "Set-NetTCPSetting -ScalingHeuristics",
            ApplyCommand = $"{SetTcp} -ScalingHeuristics {{0}} -ErrorAction Stop",
            Impact = "Stops Windows overriding your tuning level",
        },
        new PowerShellTweak
        {
            Id = "tcp.delayedackfreq",
            Name = "Delayed ACK frequency",
            Description =
                "How many segments arrive before Windows sends an acknowledgement. Setting it to 1 acknowledges every single packet immediately.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Moderate,
            OptimizedValue = "1",
            StateKey = "tcp.DelayedAckFrequency",
            TargetLabel = "Set-NetTCPSetting -DelayedAckFrequency",
            ApplyCommand = $"{SetTcp} -DelayedAckFrequency {{0}} -ErrorAction Stop",
            Impact = "No waiting before acknowledging a packet",
        },
        new PowerShellTweak
        {
            Id = "tcp.delayedacktimeout",
            Name = "Delayed ACK timeout",
            Description =
                "The maximum time an acknowledgement may sit in the queue waiting for company. The Windows default of 40 ms is pure added latency for a game.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Moderate,
            OptimizedValue = "10",
            StateKey = "tcp.DelayedAckTimeout",
            TargetLabel = "Set-NetTCPSetting -DelayedAckTimeoutMs",
            ApplyCommand = $"{SetTcp} -DelayedAckTimeoutMs {{0}} -ErrorAction Stop",
            Impact = "Up to 30 ms less delay per acknowledgement",
        },
        new PowerShellTweak
        {
            Id = "tcp.cwndrestart",
            Name = "Congestion window restart",
            Description =
                "Resets the congestion window after the connection sits idle. Minecraft traffic is bursty, so restarting the window keeps re-slowing the stream.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Safe,
            OptimizedValue = "False",
            StateKey = "tcp.CwndRestart",
            TargetLabel = "Set-NetTCPSetting -CwndRestart",
            ApplyCommand = $"{SetTcp} -CwndRestart ${{0}} -ErrorAction Stop",
            Impact = "No slow-start penalty after idle moments",
        },
        new PowerShellTweak
        {
            Id = "tcp.forcews",
            Name = "Force window scaling",
            Description = "Keeps the TCP window scale option enabled even when a middlebox strips it from the handshake.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Safe,
            OptimizedValue = "Enabled",
            StateKey = "tcp.ForceWS",
            TargetLabel = "Set-NetTCPSetting -ForceWS",
            ApplyCommand = $"{SetTcp} -ForceWS {{0}} -ErrorAction Stop",
            Impact = "Reliable throughput on long links",
        },
        new PowerShellTweak
        {
            Id = "tcp.memorypressure",
            Name = "Memory pressure protection",
            Description =
                "Drops connections when the TCP stack is short on memory. It is a server safeguard that only adds a check on a desktop.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Advanced,
            OptimizedValue = "Disabled",
            StateKey = "tcp.MemoryPressureProtection",
            TargetLabel = "Set-NetTCPSetting -MemoryPressureProtection",
            ApplyCommand = $"{SetTcp} -MemoryPressureProtection {{0}} -ErrorAction Stop",
            Impact = "One less check in the receive path",
        },
        new PowerShellTweak
        {
            Id = "offload.rsc",
            Name = "Receive segment coalescing",
            Description =
                "Merges several incoming packets into one before handing them to Windows. Great for file transfers, terrible for a game that wants each packet immediately.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Safe,
            OptimizedValue = "Disabled",
            StateKey = "offload.ReceiveSegmentCoalescing",
            TargetLabel = "Set-NetOffloadGlobalSetting -ReceiveSegmentCoalescing",
            ApplyCommand = $"{SetOffload} -ReceiveSegmentCoalescing {{0}} -ErrorAction Stop",
            Impact = "Packets reach the game without being batched",
        },
        new PowerShellTweak
        {
            Id = "offload.rss",
            Name = "Receive side scaling",
            Description =
                "Spreads incoming packet processing across CPU cores instead of pinning it all to core 0.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Safe,
            OptimizedValue = "Enabled",
            StateKey = "offload.ReceiveSideScaling",
            TargetLabel = "Set-NetOffloadGlobalSetting -ReceiveSideScaling",
            ApplyCommand = $"{SetOffload} -ReceiveSideScaling {{0}} -ErrorAction Stop",
            Impact = "Packet handling no longer bottlenecks one core",
        },
        new PowerShellTweak
        {
            Id = "offload.chimney",
            Name = "TCP chimney offload",
            Description =
                "A deprecated feature that hands whole connections to the network card. Modern drivers implement it inconsistently and it is off by default for a reason.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Safe,
            OptimizedValue = "Disabled",
            StateKey = "offload.Chimney",
            TargetLabel = "Set-NetOffloadGlobalSetting -Chimney",
            ApplyCommand = $"{SetOffload} -Chimney {{0}} -ErrorAction Stop",
            Impact = "Avoids a legacy offload path",
        },
        new PowerShellTweak
        {
            Id = "offload.pcf",
            Name = "Packet coalescing filter",
            Description = "Lets the adapter hold packets back while the system is idle so it can wake up less often.",
            Category = TweakCategory.TcpStack,
            Risk = TweakRisk.Safe,
            OptimizedValue = "Disabled",
            StateKey = "offload.PacketCoalescingFilter",
            TargetLabel = "Set-NetOffloadGlobalSetting -PacketCoalescingFilter",
            ApplyCommand = $"{SetOffload} -PacketCoalescingFilter {{0}} -ErrorAction Stop",
            Impact = "No power-saving packet batching",
        },
        new RegistryTweak
        {
            Id = "iface.tcpackfrequency",
            Name = "Immediate acknowledgements",
            Description =
                "Disables delayed ACK on this adapter so Windows answers every packet at once instead of waiting for a second one to pair it with.",
            Category = TweakCategory.Interface,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "1",
            Scope = RegistryScope.Interface,
            KeyPath = string.Empty,
            ValueName = "TcpAckFrequency",
            Impact = "The single biggest delayed-ACK win",
        },
        new RegistryTweak
        {
            Id = "iface.tcpnodelay",
            Name = "Disable Nagle's algorithm",
            Description =
                "Nagle holds small packets back to combine them into bigger ones. Every Minecraft movement and hit packet is small, so it is exactly the wrong thing to batch.",
            Category = TweakCategory.Interface,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "1",
            Scope = RegistryScope.Interface,
            KeyPath = string.Empty,
            ValueName = "TCPNoDelay",
            Impact = "Hit and movement packets leave instantly",
        },
        new RegistryTweak
        {
            Id = "iface.tcpdelackticks",
            Name = "Delayed ACK ticks",
            Description = "Sets the delayed acknowledgement timer on this adapter to zero ticks.",
            Category = TweakCategory.Interface,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Scope = RegistryScope.Interface,
            KeyPath = string.Empty,
            ValueName = "TcpDelAckTicks",
            Impact = "Removes the remaining ACK timer",
        },
        new RegistryTweak
        {
            Id = "iface.mtu",
            Name = "Interface MTU",
            Description =
                "The largest packet this adapter will send without fragmenting. Matching it to your connection type avoids fragmentation, which costs a full round trip when it happens.",
            Category = TweakCategory.Interface,
            Risk = TweakRisk.Moderate,
            NeedsAdapter = true,
            RequiresRestart = true,
            OptimizedValue = "1500",
            Scope = RegistryScope.Interface,
            KeyPath = string.Empty,
            ValueName = "MTU",
            Impact = "No fragmented packets on the wire",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.interruptmoderation",
            Name = "Interrupt moderation",
            Description =
                "The adapter waits and collects packets before interrupting the CPU. That wait is added directly to your ping.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Keyword = "*InterruptModeration",
            Impact = "Packets interrupt the CPU the moment they land",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.flowcontrol",
            Name = "Flow control",
            Description =
                "Lets the switch tell your adapter to pause sending entirely. A pause frame stalls your traffic for milliseconds at a time.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Keyword = "*FlowControl",
            Impact = "No pause frames stalling your uplink",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.lso4",
            Name = "Large send offload v2 (IPv4)",
            Description = "Hands large buffers to the adapter to split up. It helps bulk transfer and delays small packets behind it.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Keyword = "*LsoV2IPv4",
            Impact = "Small packets stop queueing behind big ones",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.lso6",
            Name = "Large send offload v2 (IPv6)",
            Description = "The IPv6 half of large send offload.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Keyword = "*LsoV2IPv6",
            Impact = "Same benefit on IPv6 routes",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.rsc4",
            Name = "Receive coalescing (IPv4)",
            Description = "The adapter-level version of receive segment coalescing, set per driver.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Keyword = "*RscIPv4",
            Impact = "No batching in the driver either",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.rsc6",
            Name = "Receive coalescing (IPv6)",
            Description = "The IPv6 half of adapter receive coalescing.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Keyword = "*RscIPv6",
            Impact = "Same benefit on IPv6 routes",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.jumbo",
            Name = "Jumbo frames",
            Description =
                "Frames larger than 1514 bytes only work if every hop supports them. On a home network they cause silent drops.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "1514",
            Keyword = "*JumboPacket",
            Impact = "Standard frame size, no silent drops",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.eee",
            Name = "Energy efficient ethernet",
            Description =
                "Puts the link into a low power state between packets. Waking it back up takes time that lands on your first packet.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Keyword = "*EEE",
            Impact = "The link never sleeps mid-fight",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.greenethernet",
            Name = "Green ethernet",
            Description = "Realtek's own power saving mode, with the same wake-up cost as energy efficient ethernet.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Keyword = "EnableGreenEthernet",
            Impact = "No vendor power saving on the link",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.gigalite",
            Name = "Gigabit Lite",
            Description = "Drops the link to a lower power gigabit mode. Saves a fraction of a watt and costs consistency.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Keyword = "GigaLite",
            Impact = "Full speed link at all times",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.powersaving",
            Name = "Adapter power saving mode",
            Description = "Driver level power saving that throttles the adapter when it thinks you are idle.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Keyword = "PowerSavingMode",
            Impact = "Adapter stays at full readiness",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.advancedeee",
            Name = "Advanced EEE",
            Description = "An extra aggressive variant of energy efficient ethernet found on some Realtek drivers.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Keyword = "AdvancedEEE",
            Impact = "Removes the deepest link sleep state",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.ulpmode",
            Name = "Ultra low power mode",
            Description = "Another vendor power state that parks the PHY between bursts of traffic.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Keyword = "ULPMode",
            Impact = "PHY stays awake",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.receivebuffers",
            Name = "Receive buffers",
            Description =
                "How many descriptors the adapter keeps for incoming packets. More buffers absorb bursts instead of dropping them.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Advanced,
            NeedsAdapter = true,
            OptimizedValue = "1024",
            Keyword = "*ReceiveBuffers",
            Impact = "Fewer drops during traffic bursts",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.transmitbuffers",
            Name = "Transmit buffers",
            Description = "The outgoing equivalent of receive buffers.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Advanced,
            NeedsAdapter = true,
            OptimizedValue = "512",
            Keyword = "*TransmitBuffers",
            Impact = "Smoother outbound bursts",
        },
        new AdapterPropertyTweak
        {
            Id = "nic.priorityvlan",
            Name = "Priority and VLAN tagging",
            Description = "802.1p and VLAN tag processing. Unused on a normal home network and it costs a lookup per packet.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Advanced,
            NeedsAdapter = true,
            OptimizedValue = "0",
            Keyword = "*PriorityVlanTag",
            Impact = "One less per-packet lookup",
        },
        new RegistryTweak
        {
            Id = "nic.pnpcapabilities",
            Name = "Adapter power management",
            Description =
                "Stops Windows from being allowed to turn the network adapter off to save power, which is the setting hidden in Device Manager.",
            Category = TweakCategory.Adapter,
            Risk = TweakRisk.Safe,
            NeedsAdapter = true,
            RequiresRestart = true,
            OptimizedValue = "24",
            Scope = RegistryScope.AdapterClass,
            KeyPath = string.Empty,
            ValueName = "PnPCapabilities",
            Impact = "Windows can never power down the NIC",
        },
        new RegistryTweak
        {
            Id = "sys.networkthrottling",
            Name = "Network throttling index",
            Description =
                "Windows caps non-multimedia network traffic at about 10 packets per millisecond so audio never stutters. Disabling the cap removes an artificial ceiling on game traffic.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "4294967295",
            Scope = RegistryScope.Machine,
            KeyPath = SystemProfile,
            ValueName = "NetworkThrottlingIndex",
            Impact = "No packet-rate ceiling on game traffic",
        },
        new RegistryTweak
        {
            Id = "sys.systemresponsiveness",
            Name = "System responsiveness",
            Description =
                "The share of CPU that Windows reserves for background multimedia work. Lowering it gives foreground applications more of the scheduler.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "10",
            Scope = RegistryScope.Machine,
            KeyPath = SystemProfile,
            ValueName = "SystemResponsiveness",
            Impact = "More CPU headroom for the game",
        },
        new RegistryTweak
        {
            Id = "sys.nolazymode",
            Name = "Multimedia lazy mode",
            Description = "Stops the multimedia class scheduler from entering its relaxed timing mode.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Advanced,
            RequiresRestart = true,
            OptimizedValue = "1",
            Scope = RegistryScope.Machine,
            KeyPath = SystemProfile,
            ValueName = "NoLazyMode",
            Impact = "Tighter scheduler timing",
        },
        new RegistryTweak
        {
            Id = "sys.defaultttl",
            Name = "Default TTL",
            Description = "The hop limit stamped on outgoing packets. 64 is the standard value and avoids odd routing behaviour.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "64",
            Scope = RegistryScope.Machine,
            KeyPath = TcpParameters,
            ValueName = "DefaultTTL",
            Impact = "Standards-correct hop limit",
        },
        new RegistryTweak
        {
            Id = "sys.timedwait",
            Name = "TCP timed wait delay",
            Description =
                "How long a closed connection keeps its port reserved. The 240 second default can exhaust ports when a client reconnects repeatedly.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "30",
            Scope = RegistryScope.Machine,
            KeyPath = TcpParameters,
            ValueName = "TcpTimedWaitDelay",
            Impact = "Ports free up quickly on reconnect",
        },
        new RegistryTweak
        {
            Id = "sys.maxuserport",
            Name = "Maximum user ports",
            Description = "Raises the ceiling on simultaneous outbound connections.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "65534",
            Scope = RegistryScope.Machine,
            KeyPath = TcpParameters,
            ValueName = "MaxUserPort",
            Impact = "No port exhaustion",
        },
        new RegistryTweak
        {
            Id = "sys.sackopts",
            Name = "Selective acknowledgements",
            Description =
                "Lets the receiver say exactly which packets are missing instead of forcing a resend of everything after the gap.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "1",
            Scope = RegistryScope.Machine,
            KeyPath = TcpParameters,
            ValueName = "SackOpts",
            Impact = "Only the lost packet is resent",
        },
        new RegistryTweak
        {
            Id = "sys.tcp1323",
            Name = "TCP window scaling option",
            Description = "Enables window scaling while leaving timestamps off.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "1",
            Scope = RegistryScope.Machine,
            KeyPath = TcpParameters,
            ValueName = "Tcp1323Opts",
            Impact = "Window scaling without timestamp overhead",
        },
        new RegistryTweak
        {
            Id = "sys.pmtu",
            Name = "Path MTU discovery",
            Description = "Lets Windows find the largest packet size that survives the whole route without fragmenting.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "1",
            Scope = RegistryScope.Machine,
            KeyPath = TcpParameters,
            ValueName = "EnablePMTUDiscovery",
            Impact = "Avoids blind fragmentation",
        },
        new RegistryTweak
        {
            Id = "sys.qoslimit",
            Name = "Reserved QoS bandwidth",
            Description =
                "Windows reserves a slice of your bandwidth for QoS-aware applications. Setting the reservation to zero returns it to you.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "0",
            Scope = RegistryScope.Machine,
            KeyPath = @"SOFTWARE\Policies\Microsoft\Windows\Psched",
            ValueName = "NonBestEffortLimit",
            Impact = "Full bandwidth available to you",
        },
        new RegistryTweak
        {
            Id = "sys.qosnla",
            Name = "QoS network location awareness",
            Description = "Stops QoS policies from waiting on network location detection before they take effect.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "1",
            Scope = RegistryScope.Machine,
            KeyPath = @"SOFTWARE\Policies\Microsoft\Windows\QoS",
            ValueName = "Do not use NLA",
            ValueKind = RegistryValueKind.String,
            Impact = "QoS applies without waiting",
        },
        new RegistryTweak
        {
            Id = "sys.deliveryoptimization",
            Name = "Delivery optimization",
            Description =
                "Windows Update peer-to-peer sharing, which uploads update data to strangers in the background using your connection.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "0",
            Scope = RegistryScope.Machine,
            KeyPath = @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
            ValueName = "DODownloadMode",
            Impact = "No background upload stealing bandwidth",
        },
        new RegistryTweak
        {
            Id = "game.gpupriority",
            Name = "Games GPU priority",
            Description = "The GPU scheduling priority the multimedia scheduler assigns to tasks registered as games.",
            Category = TweakCategory.Gaming,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "8",
            Scope = RegistryScope.Machine,
            KeyPath = GamesTask,
            ValueName = "GPU Priority",
            Impact = "Games outrank background GPU work",
        },
        new RegistryTweak
        {
            Id = "game.cpupriority",
            Name = "Games CPU priority",
            Description = "The CPU scheduling priority for the Games multimedia task profile.",
            Category = TweakCategory.Gaming,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "6",
            Scope = RegistryScope.Machine,
            KeyPath = GamesTask,
            ValueName = "Priority",
            Impact = "Games outrank background CPU work",
        },
        new RegistryTweak
        {
            Id = "game.schedulingcategory",
            Name = "Games scheduling category",
            Description = "Moves the Games profile into the high scheduling category.",
            Category = TweakCategory.Gaming,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "High",
            Scope = RegistryScope.Machine,
            KeyPath = GamesTask,
            ValueName = "Scheduling Category",
            ValueKind = RegistryValueKind.String,
            Impact = "Higher scheduler class for games",
        },
        new RegistryTweak
        {
            Id = "game.sfiopriority",
            Name = "Games storage IO priority",
            Description = "Raises disk IO priority for the Games profile so chunk loading is not starved.",
            Category = TweakCategory.Gaming,
            Risk = TweakRisk.Safe,
            RequiresRestart = true,
            OptimizedValue = "High",
            Scope = RegistryScope.Machine,
            KeyPath = GamesTask,
            ValueName = "SFIO Priority",
            ValueKind = RegistryValueKind.String,
            Impact = "Chunk loading is not starved by background IO",
        },
        new RegistryTweak
        {
            Id = "game.javawcpu",
            Name = "Java process CPU priority",
            Description =
                "Starts javaw.exe at high priority every time it launches, so the Minecraft client is never descheduled behind background work.",
            Category = TweakCategory.Gaming,
            Risk = TweakRisk.Moderate,
            OptimizedValue = "3",
            Scope = RegistryScope.Machine,
            KeyPath = $@"{ImageFileOptions}\javaw.exe\PerfOptions",
            ValueName = "CpuPriorityClass",
            Impact = "Minecraft always starts at high priority",
        },
        new RegistryTweak
        {
            Id = "game.javawio",
            Name = "Java process IO priority",
            Description = "Gives javaw.exe a high IO priority class at launch.",
            Category = TweakCategory.Gaming,
            Risk = TweakRisk.Moderate,
            OptimizedValue = "3",
            Scope = RegistryScope.Machine,
            KeyPath = $@"{ImageFileOptions}\javaw.exe\PerfOptions",
            ValueName = "IoPriority",
            Impact = "Faster world loading under disk pressure",
        },
        new RegistryTweak
        {
            Id = "game.gamedvr",
            Name = "Game DVR background recording",
            Description =
                "The Xbox Game Bar capture pipeline hooks every fullscreen game and keeps a rolling recording buffer, costing frames and CPU.",
            Category = TweakCategory.Gaming,
            Risk = TweakRisk.Safe,
            OptimizedValue = "0",
            Scope = RegistryScope.User,
            KeyPath = @"System\GameConfigStore",
            ValueName = "GameDVR_Enabled",
            Impact = "No background capture hooking the game",
        },
        new RegistryTweak
        {
            Id = "game.gamedvrfse",
            Name = "Fullscreen optimizations hook",
            Description = "Stops Game DVR from forcing its fullscreen behaviour on every game window.",
            Category = TweakCategory.Gaming,
            Risk = TweakRisk.Safe,
            OptimizedValue = "2",
            Scope = RegistryScope.User,
            KeyPath = @"System\GameConfigStore",
            ValueName = "GameDVR_FSEBehaviorMode",
            Impact = "True fullscreen presentation path",
        },
        new RegistryTweak
        {
            Id = "game.automode",
            Name = "Windows Game Mode",
            Description = "Keeps Game Mode on so Windows deprioritises background services while a game is in focus.",
            Category = TweakCategory.Gaming,
            Risk = TweakRisk.Safe,
            OptimizedValue = "1",
            Scope = RegistryScope.User,
            KeyPath = @"SOFTWARE\Microsoft\GameBar",
            ValueName = "AutoGameModeEnabled",
            Impact = "Background services yield to the game",
        },
    ];
}
