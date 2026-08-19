using ProjectAgil.Models;

namespace ProjectAgil.Services;

public interface IPlanBuilder
{
    IReadOnlyList<PlanItem> Build(OptimizationProfile profile, TweakContext ctx);
}

public sealed class PlanBuilder(ITweakCatalog catalog) : IPlanBuilder
{
    public IReadOnlyList<PlanItem> Build(OptimizationProfile profile, TweakContext ctx)
    {
        var items = new List<PlanItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var latency = Math.Clamp(profile.Latency, 0, 100);
        var responsiveness = Math.Clamp(profile.Responsiveness, 0, 100);
        var stable = profile.StableConnection;

        void Add(string id, string value, string reason)
        {
            if (!seen.Add(id))
            {
                return;
            }

            var tweak = catalog.Find(id);
            if (tweak is null)
            {
                return;
            }

            if (tweak.NeedsAdapter && ctx.Adapter is null)
            {
                return;
            }

            if (profile.ExcludedTweaks.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            if (tweak.Risk == TweakRisk.Advanced && !profile.IncludeAdvanced)
            {
                return;
            }

            var current = tweak.Read(ctx);

            items.Add(
                new PlanItem
                {
                    Tweak = tweak,
                    DesiredValue = value,
                    Reason = reason,
                    CurrentValue = current,
                    Status = current is null
                        ? TweakStatus.NotOptimized
                        : tweak.Matches(current, value)
                            ? TweakStatus.Optimized
                            : TweakStatus.NotOptimized,
                }
            );
        }

        Add("tcp.autotuning", profile.Tuning.ToNetshValue(), $"Tuning is set to {profile.Tuning.ToDisplay()}");
        Add("tcp.scalingheuristics", "Disabled", "Keeps the tuning level you chose from being overridden");

        var mtu = profile.Connection switch
        {
            ConnectionType.Dsl => "1492",
            ConnectionType.Mobile => "1428",
            _ => "1500",
        };

        var congestion = profile.Connection switch
        {
            ConnectionType.Satellite => "CTCP",
            ConnectionType.Mobile => "CTCP",
            _ => stable ? "CUBIC" : "NewReno",
        };

        Add("tcp.congestion", congestion, $"Chosen for a {profile.Connection.ToDisplay()} link that is {(stable ? "stable" : "unstable")}");
        Add("iface.mtu", mtu, $"Matches a {profile.Connection.ToDisplay()} connection");

        if (profile.SmartPackets)
        {
            Add("iface.tcpackfrequency", "1", "Smart packets acknowledges every packet immediately");
            Add("iface.tcpnodelay", "1", "Smart packets disables Nagle batching");
            Add("iface.tcpdelackticks", "0", "Smart packets clears the delayed ACK timer");
            Add("tcp.delayedackfreq", "1", "Smart packets acknowledges every segment");
            Add("tcp.delayedacktimeout", latency >= 60 ? "10" : "20", "Smart packets shortens the ACK hold time");
        }

        if (latency >= 10)
        {
            Add("offload.rsc", "Disabled", "Latency: incoming packets are not batched");
        }

        if (latency >= 15)
        {
            Add("nic.rsc4", "0", "Latency: driver level coalescing off");
            Add("nic.rsc6", "0", "Latency: driver level coalescing off");
        }

        if (latency >= 20)
        {
            Add("offload.rss", "Enabled", "Latency: spread packet processing across cores");
        }

        if (latency >= 25)
        {
            Add("nic.interruptmoderation", "0", "Latency: the adapter interrupts the CPU immediately");
        }

        if (latency >= 30)
        {
            Add("offload.chimney", "Disabled", "Latency: avoids the legacy offload path");
            Add("sys.pmtu", "1", "Latency: discover the real path MTU");
            Add("sys.sackopts", "1", "Latency: resend only what was lost");
            Add("sys.tcp1323", "1", "Latency: window scaling without timestamps");
        }

        if (latency >= 35)
        {
            Add(
                "tcp.nonsack",
                stable ? "Disabled" : "Enabled",
                stable ? "Latency: legacy resiliency not needed" : "Kept on because the connection is unstable"
            );
        }

        if (latency >= 40)
        {
            Add("nic.flowcontrol", "0", "Latency: no pause frames from the switch");
            Add("offload.pcf", "Disabled", "Latency: no power saving packet batching");
        }

        if (latency >= 45)
        {
            Add(
                "tcp.ecn",
                stable ? "Disabled" : "Enabled",
                stable ? "Latency: avoids badly implemented router marking" : "Kept on to signal congestion on an unstable link"
            );
        }

        if (latency >= 50)
        {
            Add("tcp.timestamps", "Disabled", "Latency: smaller packets");
            Add("nic.jumbo", "1514", "Latency: standard frame size");
        }

        if (latency >= 55)
        {
            Add(
                "tcp.minrto",
                stable ? (latency >= 80 ? "200" : "300") : "500",
                stable ? "Latency: retransmit sooner after a drop" : "Held higher because the connection is unstable"
            );
        }

        if (latency >= 60)
        {
            Add("tcp.initialrto", stable ? "500" : "1000", "Latency: faster first retransmit when joining a server");
            Add("nic.lso4", "0", "Latency: small packets do not queue behind large ones");
            Add("nic.lso6", "0", "Latency: small packets do not queue behind large ones");
        }

        if (latency >= 65)
        {
            Add("tcp.maxsyn", stable ? "2" : "4", "Latency: fail a dead connection fast");
        }

        if (latency >= 70)
        {
            Add("tcp.cwndrestart", "False", "Latency: no slow start after idle moments");
            Add("tcp.forcews", "Enabled", "Latency: keep window scaling active");
        }

        if (latency >= 75)
        {
            Add("nic.eee", "0", "Latency: the link never sleeps");
            Add("nic.greenethernet", "0", "Latency: the link never sleeps");
            Add("nic.gigalite", "0", "Latency: full speed link at all times");
            Add("nic.powersaving", "0", "Latency: adapter stays at full readiness");
            Add("nic.advancedeee", "0", "Latency: no deep link sleep");
            Add("nic.ulpmode", "0", "Latency: the PHY stays awake");
            Add("nic.pnpcapabilities", "24", "Latency: Windows may not power down the adapter");
        }

        if (latency >= 80)
        {
            Add("sys.defaultttl", "64", "Latency: standards correct hop limit");
            Add("sys.timedwait", "30", "Latency: ports free up quickly on reconnect");
            Add("sys.maxuserport", "65534", "Latency: no port exhaustion");
        }

        if (latency >= 85)
        {
            var icw = stable && profile.Connection is ConnectionType.Fiber or ConnectionType.Cable ? "16" : "10";
            Add("tcp.icw", icw, "Latency: larger first burst on a fast link");
        }

        if (latency >= 90)
        {
            Add("tcp.memorypressure", "Disabled", "Latency: removes a server safeguard from the receive path");
            Add("nic.receivebuffers", "1024", "Latency: absorb bursts instead of dropping them");
            Add("nic.transmitbuffers", "512", "Latency: smoother outbound bursts");
            Add("nic.priorityvlan", "0", "Latency: no VLAN tag processing per packet");
        }

        if (responsiveness >= 10)
        {
            Add("sys.qoslimit", "0", "Responsiveness: reclaim the reserved QoS bandwidth");
        }

        if (responsiveness >= 20)
        {
            Add("sys.qosnla", "1", "Responsiveness: QoS applies without waiting on location detection");
        }

        if (responsiveness >= 30)
        {
            Add(
                "sys.systemresponsiveness",
                responsiveness >= 70 ? "0" : "10",
                "Responsiveness: less CPU reserved for background multimedia"
            );
        }

        if (responsiveness >= 40)
        {
            Add("sys.networkthrottling", "4294967295", "Responsiveness: no packet rate ceiling on game traffic");
        }

        if (responsiveness >= 50)
        {
            Add("game.gpupriority", "8", "Responsiveness: games outrank background GPU work");
            Add("game.cpupriority", "6", "Responsiveness: games outrank background CPU work");
            Add("game.schedulingcategory", "High", "Responsiveness: high scheduler class for games");
            Add("game.sfiopriority", "High", "Responsiveness: chunk loading is not starved");
        }

        if (responsiveness >= 60)
        {
            Add("game.gamedvr", "0", "Responsiveness: no background capture hooking the game");
            Add("game.gamedvrfse", "2", "Responsiveness: true fullscreen presentation path");
        }

        if (responsiveness >= 65)
        {
            Add("game.automode", "1", "Responsiveness: Game Mode keeps services out of the way");
        }

        if (responsiveness >= 70)
        {
            Add("game.javawcpu", "3", "Responsiveness: Minecraft launches at high priority");
            Add("game.javawio", "3", "Responsiveness: Minecraft gets high IO priority");
        }

        if (responsiveness >= 80)
        {
            Add("sys.deliveryoptimization", "0", "Responsiveness: no background update uploads");
        }

        if (responsiveness >= 90)
        {
            Add("sys.nolazymode", "1", "Responsiveness: tighter multimedia scheduler timing");
        }

        return items;
    }
}
