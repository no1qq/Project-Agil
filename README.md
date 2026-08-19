# Project-Agil

A free, fully local network optimizer for Minecraft PvP.

Project-Agil tunes the parts of the Windows network stack that decide how fast a hit
packet leaves your machine and how fast the server's reply reaches your game. It is a
free alternative to Ghast Lightning, with three differences that matter:

- **No account, no licence key, no telemetry.** Nothing is sent anywhere. There is no
  server to talk to.
- **Nothing is hidden.** Every change shows you the exact registry key, PowerShell
  command or driver property it will touch, its current value and its target value,
  before anything is written.
- **Everything is reversible.** The previous value of every setting is written to an
  undo point before it is changed. One button puts all of it back.

## Quick start

1. Grab the installer or the portable exe from
   [Releases](https://github.com/no1qq/Project-Agil/releases), or build it yourself
   (see [Building](#building)).
2. Launch it. Windows asks for administrator rights, and the app cannot do its job
   without them.
3. Open **Optimize**, pick a preset, read the plan on the right, press
   *Apply these changes*.
4. Not happy? **Undo points** puts everything back exactly as it was.

## What it actually changes

61 settings across six categories, applied per network card where that matters:

| Category | Examples |
|---|---|
| TCP stack | receive window auto-tuning, congestion provider, ECN, timestamps, MinRTO, initial RTO, SYN retransmissions, initial congestion window, delayed ACK frequency and timeout, SACK resiliency, scaling heuristics |
| Offload | receive segment coalescing, receive side scaling, chimney offload, packet coalescing filter |
| Interface | `TcpAckFrequency`, `TCPNoDelay`, `TcpDelAckTicks`, per-adapter MTU |
| Network card | interrupt moderation, flow control, large send offload, jumbo frames, energy efficient ethernet, green ethernet, adapter power management, receive and transmit buffers |
| System | network throttling index, system responsiveness, default TTL, timed wait delay, max user ports, reserved QoS bandwidth, delivery optimization |
| Gaming | Games task GPU/CPU/IO priority, Game DVR, fullscreen optimizations, Game Mode, `javaw.exe` process priority |

### Presets

| Preset | For |
|---|---|
| **Safe** | Conservative. Nothing driver specific, nothing that needs a restart to make sense. |
| **Balanced** | The everyday setting. Lower latency without stripping recovery behaviour. |
| **PvP** | Everything aimed at reaction time, driver tweaks included. |
| **Shaky link** | Wi-Fi or a line that drops packets. Keeps the settings that help a bad connection. |

The controls under the presets are not decoration. **Send packets immediately**,
**Cut waiting time** and **Prioritise the game** decide which tweaks enter the plan and
at what value, **Receive buffer** maps directly to
`Set-NetTCPSetting -AutoTuningLevelLocal`, **Internet type** picks the MTU and the
congestion algorithm, and **My connection is stable** decides whether resiliency
settings are stripped or kept. Move anything and the plan on the right rebuilds live.

## The pages

- **Dashboard** — how much of your plan is already applied, which card you are playing
  on, and a live latency graph against your main server.
- **Optimize** — the control panel. Presets, sliders, card picker, and the full plan
  grouped by category before anything is written.
- **All settings** — every one of the 61 tweaks on its own row, with its live current
  value, what it would become, a search box, and per-setting apply, revert and exclude.
- **Watch my ping** — live ping to as many servers as you like, with jitter, packet
  loss, min/max, a scrolling graph with loss marked in red, and CSV export.
- **Network cards** — card details, MTU, DNS presets (Cloudflare, Google, Quad9,
  AdGuard) and the raw driver property table.
- **Saved setups** — store the choices you made on Optimize under a name, switch
  between them, export and import them.
- **Undo points** — every change ever made, what it was before, and one-click revert.
- **Fix and check** — flush DNS, clear ARP, renew the DHCP lease, reset Winsock, reset
  TCP/IP, trace a route, and export a full network report.
- **Settings** — how the app itself behaves. Nothing here touches your network.

## Ping the way the game measures it

ICMP is the wrong tool for a big Minecraft server. Hypixel and friends sit behind an
anycast proxy, so a normal ping hits the nearest edge node and never reaches the game
server: from Germany `mc.hypixel.net` answers ICMP in about 23 ms, while the number the
game shows you is around 120 ms.

Rows marked **Minecraft** on the *Watch my ping* page speak Server List Ping, the same
protocol the multiplayer list uses, so the measurement includes the proxy hop and the
server tick and matches what you see in game. Those rows deliberately update every few
seconds rather than every second, because game servers rate limit repeated connections
and start refusing you. A refusal is shown as a refusal, never counted as packet loss.

## Requirements

- Windows 10 1809 or newer, x64
- Administrator rights (the app requests elevation at launch; writing TCP and card
  settings is impossible without it)
- .NET 8 Desktop Runtime, unless you use the portable build

## Building

```
git clone https://github.com/no1qq/Project-Agil.git
cd Project-Agil
dotnet build
```

Run from source:

```
dotnet run --project src/ProjectAgil
```

### Distribution builds

```
build\build-all.bat
```

produces both artifacts in `dist\`:

| Artifact | Path | Notes |
|---|---|---|
| Installer | `dist\Project-Agil-Setup-1.0.0.exe` | Framework dependent, roughly 10 MB. Installs the app and optionally the full source tree, creates shortcuts and an uninstaller, and downloads the .NET 8 Desktop Runtime from Microsoft if the machine does not have it. Requires [Inno Setup 6](https://jrsoftware.org/isdl.php) to compile. |
| Portable | `dist\portable\Project-Agil.exe` | Self contained single file, roughly 150 MB. The runtime, WPF and every dependency are inside the exe. No installer, no dependencies, nothing to set up. |

The two can also be built separately with `build\build-installer.bat` and
`build\build-portable.bat`.

## Where your data lives

`%APPDATA%\Project-Agil\`

```
settings.json          application settings
active-profile.json    the setup currently loaded in the optimizer
profiles\              saved setups
backups\               undo points, one JSON file per optimization run
logs\                  crash logs
```

Nothing is written anywhere else, and nothing leaves the machine.

## Safety

Every write goes through the same path: read the current value, record it in an undo
point, then write. A setting that did not exist before is recorded as such, so reverting
removes it again instead of leaving a stray key behind. If a setting does not exist on
your hardware, for example a driver that does not expose energy efficient ethernet, it
is skipped and reported rather than forced.

Settings marked **Advanced** (driver buffer sizes, multimedia scheduler internals,
memory pressure protection) stay out of the plan unless you turn on *Include driver
tweaks*.

A few settings only take effect after a restart. The optimizer tells you how many of
those are in your plan before you run it, and shows a notice afterwards.

## Licence

MIT.

WPF UI by [lepoco](https://github.com/lepoco/wpfui) is used under the MIT licence.
