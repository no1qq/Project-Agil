using Microsoft.Win32;
using ProjectAgil.Services;

namespace ProjectAgil.Models;

public sealed class TweakContext
{
    public required IRegistryService Registry { get; init; }

    public required IProcessRunner Process { get; init; }

    public NetworkAdapterInfo? Adapter { get; init; }

    public IReadOnlyDictionary<string, string> State { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public Action<string>? Log { get; init; }

    public string? StateValue(string key) =>
        State.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}

public abstract class Tweak
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string OptimizedValue { get; init; }

    public TweakCategory Category { get; init; } = TweakCategory.System;

    public TweakRisk Risk { get; init; } = TweakRisk.Safe;

    public bool RequiresRestart { get; init; }

    public bool NeedsAdapter { get; init; }

    public string Impact { get; init; } = string.Empty;

    public abstract string Kind { get; }

    public abstract string Target(TweakContext ctx);

    public abstract string? Read(TweakContext ctx);

    public abstract Task<BackupEntry?> ApplyAsync(TweakContext ctx, string value, CancellationToken ct);

    public abstract Task RestoreAsync(TweakContext ctx, BackupEntry entry, CancellationToken ct);

    public virtual bool Matches(string? current, string desired) =>
        current is not null && string.Equals(current.Trim(), desired.Trim(), StringComparison.OrdinalIgnoreCase);

    public TweakStatus Evaluate(TweakContext ctx, string desired)
    {
        if (NeedsAdapter && ctx.Adapter is null)
        {
            return TweakStatus.Unsupported;
        }

        var current = Read(ctx);
        if (current is null)
        {
            return TweakStatus.NotOptimized;
        }

        return Matches(current, desired) ? TweakStatus.Optimized : TweakStatus.NotOptimized;
    }
}

public sealed class RegistryTweak : Tweak
{
    public required RegistryScope Scope { get; init; }

    public required string KeyPath { get; init; }

    public required string ValueName { get; init; }

    public RegistryValueKind ValueKind { get; init; } = RegistryValueKind.DWord;

    public override string Kind => "registry";

    public override string Target(TweakContext ctx)
    {
        var resolved = ResolvePath(ctx);
        return resolved is null ? "unavailable" : $"{RootName}\\{resolved}  ->  {ValueName}";
    }

    private string RootName => Scope == RegistryScope.User ? "HKCU" : "HKLM";

    private string? ResolvePath(TweakContext ctx)
    {
        switch (Scope)
        {
            case RegistryScope.Machine:
            case RegistryScope.User:
                return KeyPath;
            case RegistryScope.Interface:
                return ctx.Adapter is null
                    ? null
                    : $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{ctx.Adapter.Id}";
            case RegistryScope.AdapterClass:
                return ctx.Adapter is null ? null : ctx.Registry.FindAdapterClassKey(ctx.Adapter.Id);
            default:
                return null;
        }
    }

    public override string? Read(TweakContext ctx)
    {
        var path = ResolvePath(ctx);
        if (path is null)
        {
            return null;
        }

        return ctx.Registry.ReadValue(Scope == RegistryScope.User, path, ValueName);
    }

    public override Task<BackupEntry?> ApplyAsync(TweakContext ctx, string value, CancellationToken ct)
    {
        var path = ResolvePath(ctx);
        if (path is null)
        {
            return Task.FromResult<BackupEntry?>(null);
        }

        var user = Scope == RegistryScope.User;
        var previous = ctx.Registry.ReadValue(user, path, ValueName);

        var entry = new BackupEntry
        {
            TweakId = Id,
            TweakName = Name,
            Kind = Kind,
            Target = $"{(user ? "HKCU" : "HKLM")}\\{path}|{ValueName}|{ValueKind}",
            PreviousValue = previous,
            Existed = previous is not null,
            AppliedValue = value,
            AdapterId = ctx.Adapter?.Id,
            AdapterName = ctx.Adapter?.Name,
        };

        ctx.Registry.WriteValue(user, path, ValueName, value, ValueKind);
        ctx.Log?.Invoke($"{Name}: set {ValueName} = {value}");

        return Task.FromResult<BackupEntry?>(entry);
    }

    public override Task RestoreAsync(TweakContext ctx, BackupEntry entry, CancellationToken ct)
    {
        var parts = entry.Target.Split('|');
        if (parts.Length < 3)
        {
            throw new InvalidOperationException("the recorded registry target is malformed");
        }

        var user = parts[0].StartsWith("HKCU", StringComparison.OrdinalIgnoreCase);
        var path = parts[0][5..];
        var name = parts[1];
        var kind = Enum.TryParse<RegistryValueKind>(parts[2], out var parsed) ? parsed : RegistryValueKind.DWord;

        if (entry.Existed && entry.PreviousValue is not null)
        {
            ctx.Registry.WriteValue(user, path, name, entry.PreviousValue, kind);
        }
        else
        {
            ctx.Registry.DeleteValue(user, path, name);
        }

        return Task.CompletedTask;
    }
}

public sealed class PowerShellTweak : Tweak
{
    public required string StateKey { get; init; }

    public required string ApplyCommand { get; init; }

    public string TargetLabel { get; init; } = string.Empty;

    public Func<string, string>? Normalize { get; init; }

    public override string Kind => "powershell";

    public override string Target(TweakContext ctx) =>
        string.IsNullOrWhiteSpace(TargetLabel) ? StateKey : TargetLabel;

    public override string? Read(TweakContext ctx)
    {
        var raw = ctx.StateValue(StateKey);
        return raw is null ? null : Normalize?.Invoke(raw) ?? raw;
    }

    public override async Task<BackupEntry?> ApplyAsync(TweakContext ctx, string value, CancellationToken ct)
    {
        var previous = Read(ctx);
        if (previous is null)
        {
            ctx.Log?.Invoke($"{Name}: current value cannot be read on this build, skipped so it stays revertible");
            return null;
        }

        var command = string.Format(CultureInfo.InvariantCulture, ApplyCommand, value, ctx.Adapter?.Name ?? string.Empty);
        var result = await ctx.Process.RunPowerShellAsync(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            ctx.Log?.Invoke($"{Name}: skipped ({result.ShortError})");
            return null;
        }

        ctx.Log?.Invoke($"{Name}: set to {value}");

        return new BackupEntry
        {
            TweakId = Id,
            TweakName = Name,
            Kind = Kind,
            Target = ApplyCommand,
            PreviousValue = previous,
            Existed = previous is not null,
            AppliedValue = value,
            AdapterId = ctx.Adapter?.Id,
            AdapterName = ctx.Adapter?.Name,
        };
    }

    public override async Task RestoreAsync(TweakContext ctx, BackupEntry entry, CancellationToken ct)
    {
        if (!entry.Existed || entry.PreviousValue is null)
        {
            throw new InvalidOperationException("no previous value was recorded, so it cannot be put back");
        }

        var command = string.Format(
            CultureInfo.InvariantCulture,
            entry.Target,
            entry.PreviousValue,
            entry.AdapterName ?? ctx.Adapter?.Name ?? string.Empty
        );

        var result = await ctx.Process.RunPowerShellAsync(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            throw new InvalidOperationException(result.ShortError);
        }
    }
}

public sealed class AdapterPropertyTweak : Tweak
{
    public required string Keyword { get; init; }

    public IReadOnlyDictionary<string, string> ValueLabels { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public override string Kind => "adapter";

    public override string Target(TweakContext ctx) =>
        ctx.Adapter is null ? $"{Keyword}" : $"{ctx.Adapter.Name}  ->  {Keyword}";

    public override string? Read(TweakContext ctx) => ctx.StateValue($"nic.{Keyword}");

    public override async Task<BackupEntry?> ApplyAsync(TweakContext ctx, string value, CancellationToken ct)
    {
        if (ctx.Adapter is null)
        {
            return null;
        }

        var previous = Read(ctx);
        if (previous is null)
        {
            ctx.Log?.Invoke($"{Name}: not exposed by this driver, skipped");
            return null;
        }

        var command = BuildCommand(ctx.Adapter.Name, value);
        var result = await ctx.Process.RunPowerShellAsync(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            ctx.Log?.Invoke($"{Name}: skipped ({result.ShortError})");
            return null;
        }

        ctx.Log?.Invoke($"{Name}: set to {value}");

        return new BackupEntry
        {
            TweakId = Id,
            TweakName = Name,
            Kind = Kind,
            Target = Keyword,
            PreviousValue = previous,
            Existed = true,
            AppliedValue = value,
            AdapterId = ctx.Adapter.Id,
            AdapterName = ctx.Adapter.Name,
        };
    }

    public override async Task RestoreAsync(TweakContext ctx, BackupEntry entry, CancellationToken ct)
    {
        var adapter = entry.AdapterName ?? ctx.Adapter?.Name;
        if (adapter is null)
        {
            throw new InvalidOperationException("the network card this was applied to is not present");
        }

        if (entry.PreviousValue is null)
        {
            throw new InvalidOperationException("no previous value was recorded, so it cannot be put back");
        }

        var result = await ctx.Process
            .RunPowerShellAsync(BuildCommand(adapter, entry.PreviousValue), ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            throw new InvalidOperationException(result.ShortError);
        }
    }

    private string BuildCommand(string adapter, string value) =>
        $"Set-NetAdapterAdvancedProperty -Name '{adapter.Replace("'", "''")}' -RegistryKeyword '{Keyword}' -RegistryValue '{value}' -NoRestart -ErrorAction Stop";
}
