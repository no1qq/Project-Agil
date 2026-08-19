using Microsoft.Win32;

namespace ProjectAgil.Services;

public interface IRegistryService
{
    string? ReadValue(bool currentUser, string keyPath, string valueName);

    void WriteValue(bool currentUser, string keyPath, string valueName, string value, RegistryValueKind kind);

    void DeleteValue(bool currentUser, string keyPath, string valueName);

    string? FindAdapterClassKey(string adapterId);
}

public sealed class RegistryService : IRegistryService
{
    private const string NetworkClassRoot =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    private readonly Dictionary<string, string?> _adapterClassCache = new(StringComparer.OrdinalIgnoreCase);

    public string? ReadValue(bool currentUser, string keyPath, string valueName)
    {
        try
        {
            using var root = Open(currentUser, keyPath, writable: false);
            var raw = root?.GetValue(valueName);
            return Format(raw);
        }
        catch
        {
            return null;
        }
    }

    public void WriteValue(bool currentUser, string keyPath, string valueName, string value, RegistryValueKind kind)
    {
        using var key = Create(currentUser, keyPath);
        if (key is null)
        {
            throw new InvalidOperationException($"unable to open {keyPath}");
        }

        object payload = kind switch
        {
            RegistryValueKind.DWord => unchecked((int)ParseUnsigned(value)),
            RegistryValueKind.QWord => (long)ParseUnsigned(value),
            _ => value,
        };

        key.SetValue(valueName, payload, kind);
    }

    public void DeleteValue(bool currentUser, string keyPath, string valueName)
    {
        try
        {
            using var key = Open(currentUser, keyPath, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
        catch
        {
        }
    }

    public string? FindAdapterClassKey(string adapterId)
    {
        if (_adapterClassCache.TryGetValue(adapterId, out var cached))
        {
            return cached;
        }

        string? found = null;

        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(NetworkClassRoot);
            if (root is not null)
            {
                foreach (var name in root.GetSubKeyNames())
                {
                    if (!name.All(char.IsDigit))
                    {
                        continue;
                    }

                    using var child = root.OpenSubKey(name);
                    var instance = child?.GetValue("NetCfgInstanceId") as string;

                    if (string.Equals(instance, adapterId, StringComparison.OrdinalIgnoreCase))
                    {
                        found = $@"{NetworkClassRoot}\{name}";
                        break;
                    }
                }
            }
        }
        catch
        {
            found = null;
        }

        _adapterClassCache[adapterId] = found;
        return found;
    }

    private static RegistryKey? Open(bool currentUser, string keyPath, bool writable)
    {
        var hive = currentUser ? Registry.CurrentUser : Registry.LocalMachine;
        return hive.OpenSubKey(keyPath, writable);
    }

    private static RegistryKey? Create(bool currentUser, string keyPath)
    {
        var hive = currentUser ? Registry.CurrentUser : Registry.LocalMachine;
        return hive.CreateSubKey(keyPath, writable: true);
    }

    private static uint ParseUnsigned(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.Parse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        if (uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unsigned))
        {
            return unsigned;
        }

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var signed))
        {
            return unchecked((uint)signed);
        }

        return 0;
    }

    private static string? Format(object? raw) =>
        raw switch
        {
            null => null,
            int i => unchecked((uint)i).ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            string s => s,
            byte[] b => Convert.ToHexString(b),
            string[] a => string.Join(",", a),
            _ => raw.ToString(),
        };
}
