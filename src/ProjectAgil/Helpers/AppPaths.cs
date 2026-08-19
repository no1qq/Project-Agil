using System.IO;

namespace ProjectAgil.Helpers;

public static class AppPaths
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Project-Agil");

    public static string Backups { get; } = Path.Combine(Root, "backups");

    public static string Profiles { get; } = Path.Combine(Root, "profiles");

    public static string Logs { get; } = Path.Combine(Root, "logs");

    public static string SettingsFile { get; } = Path.Combine(Root, "settings.json");

    public static string ActiveProfileFile { get; } = Path.Combine(Root, "active-profile.json");

    public static void EnsureCreated()
    {
        _ = Directory.CreateDirectory(Root);
        _ = Directory.CreateDirectory(Backups);
        _ = Directory.CreateDirectory(Profiles);
        _ = Directory.CreateDirectory(Logs);
    }
}
