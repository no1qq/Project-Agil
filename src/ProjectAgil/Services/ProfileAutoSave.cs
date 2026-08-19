using System.Text.Json;

namespace ProjectAgil.Services;

public sealed class ProfileAutoSaveService(IProfileService profiles, ISettingsService settings)
    : IHostedService,
        IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private Timer? _timer;
    private string? _lastWritten;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(_ => Tick(), null, Interval, Interval);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();

    private void Tick()
    {
        if (!settings.Current.AutoSaveProfile)
        {
            return;
        }

        try
        {
            var active = profiles.Active;
            var snapshot = JsonSerializer.Serialize(active, JsonStore.Options);

            if (snapshot == _lastWritten)
            {
                return;
            }

            profiles.SaveActive();

            if (profiles.Exists(active.Name))
            {
                profiles.Save(active.Clone());
            }

            _lastWritten = snapshot;
        }
        catch
        {
        }
    }
}
