using ProjectAgil.Helpers;
using ProjectAgil.Models;

namespace ProjectAgil.Services;

public interface ISettingsService
{
    AppSettings Current { get; }

    void Save();
}

public sealed class SettingsService : ISettingsService
{
    private const int Schema = 1;

    private static readonly string[] KnownMinecraftHosts =
    [
        "mc.hypixel.net",
        "hypixel.net",
    ];

    public SettingsService()
    {
        AppPaths.EnsureCreated();
        Current = JsonStore.Read<AppSettings>(AppPaths.SettingsFile) ?? new AppSettings();

        Migrate();
    }

    public AppSettings Current { get; }

    private void Migrate()
    {
        if (Current.SchemaVersion >= Schema)
        {
            return;
        }

        foreach (var target in Current.Targets)
        {
            if (KnownMinecraftHosts.Contains(target.Host, StringComparer.OrdinalIgnoreCase))
            {
                target.Kind = PingKind.Minecraft;
            }
        }

        Current.SchemaVersion = Schema;
        Save();
    }

    public void Save() => JsonStore.Write(AppPaths.SettingsFile, Current);
}
