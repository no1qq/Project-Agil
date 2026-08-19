using System.Diagnostics;
using System.Security.Principal;

namespace ProjectAgil.Services;

public interface IElevationService
{
    bool IsElevated { get; }

    void RestartElevated();
}

public sealed class ElevationService : IElevationService
{
    public bool IsElevated
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    public void RestartElevated()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            _ = Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "runas" });
            Application.Current.Shutdown();
        }
        catch
        {
        }
    }
}

public interface IStartupService
{
    Task<bool> IsEnabledAsync(CancellationToken ct = default);

    Task<bool> SetAsync(bool enabled, CancellationToken ct = default);
}

public sealed class StartupService(IProcessRunner process) : IStartupService
{
    private const string TaskName = "Project-Agil Autostart";

    public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        var result = await process.RunAsync("schtasks.exe", $"/Query /TN \"{TaskName}\"", ct).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    public async Task<bool> SetAsync(bool enabled, CancellationToken ct = default)
    {
        if (!enabled)
        {
            var removed = await process
                .RunAsync("schtasks.exe", $"/Delete /TN \"{TaskName}\" /F", ct)
                .ConfigureAwait(false);
            return removed.ExitCode == 0;
        }

        var path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var created = await process
            .RunAsync(
                "schtasks.exe",
                $"/Create /TN \"{TaskName}\" /TR \"\\\"{path}\\\"\" /SC ONLOGON /RL HIGHEST /F",
                ct
            )
            .ConfigureAwait(false);

        return created.ExitCode == 0;
    }
}
