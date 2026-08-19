using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProjectAgil.Services;

public sealed record UpdateInfo(int Build, string AssetName, string DownloadUrl);

public interface IUpdateService
{
    int CurrentBuild { get; }

    bool IsPortable { get; }

    UpdateInfo? Available { get; }

    event EventHandler? AvailableChanged;

    Task<UpdateInfo?> CheckAsync(CancellationToken token = default);

    Task InstallAsync(UpdateInfo update, IProgress<double>? progress, CancellationToken token = default);
}

public sealed partial class UpdateService : IUpdateService
{
    public const string SetupAsset = "Project-Agil-Setup.exe";

    public const string PortableAsset = "Project-Agil-Portable.exe";

    private const string LatestRelease = "https://api.github.com/repos/no1qq/Project-Agil/releases/latest";

    private static readonly HttpClient Client = CreateClient();

    private UpdateInfo? _available;

    public int CurrentBuild { get; } = Assembly.GetExecutingAssembly().GetName().Version?.Major ?? 0;

    public bool IsPortable { get; } = !File.Exists(Path.Combine(AppFolder(), "unins000.exe"));

    public UpdateInfo? Available
    {
        get => _available;
        private set
        {
            _available = value;
            AvailableChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? AvailableChanged;

    public static int ParseBuild(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return 0;
        }

        var match = BuildTag().Match(tag.Trim());

        return match.Success && int.TryParse(match.Groups[1].Value, out var build) ? build : 0;
    }

    public async Task<UpdateInfo?> CheckAsync(CancellationToken token = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));

            using var response = await Client.GetAsync(LatestRelease, timeout.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            if (!root.TryGetProperty("tag_name", out var tag))
            {
                return null;
            }

            var build = ParseBuild(tag.GetString());

            if (build <= CurrentBuild)
            {
                Available = null;
                return null;
            }

            var wanted = IsPortable ? PortableAsset : SetupAsset;
            var url = FindAsset(root, wanted);

            if (url is null)
            {
                return null;
            }

            var info = new UpdateInfo(build, wanted, url);
            Available = info;

            return info;
        }
        catch
        {
            return null;
        }
    }

    public async Task InstallAsync(UpdateInfo update, IProgress<double>? progress, CancellationToken token = default)
    {
        var target = IsPortable
            ? StagedPortablePath()
            : Path.Combine(Path.GetTempPath(), update.AssetName);

        await DownloadAsync(update.DownloadUrl, target, progress, token).ConfigureAwait(false);

        if (IsPortable)
        {
            SwapPortable(target);
        }
        else
        {
            RunSetup(target);
        }
    }

    [GeneratedRegex("^b([0-9]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex BuildTag();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };

        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Project-Agil", "1"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        return client;
    }

    private static string AppFolder()
    {
        var path = Environment.ProcessPath;

        if (string.IsNullOrEmpty(path))
        {
            return AppContext.BaseDirectory;
        }

        return Path.GetDirectoryName(path) ?? AppContext.BaseDirectory;
    }

    private static string StagedPortablePath()
    {
        var current = Environment.ProcessPath;

        return string.IsNullOrEmpty(current)
            ? Path.Combine(Path.GetTempPath(), PortableAsset)
            : current + ".new";
    }

    private static string? FindAsset(JsonElement root, string name)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out var assetName))
            {
                continue;
            }

            if (!string.Equals(assetName.GetString(), name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (asset.TryGetProperty("browser_download_url", out var url))
            {
                return url.GetString();
            }
        }

        return null;
    }

    private static async Task DownloadAsync(
        string url,
        string path,
        IProgress<double>? progress,
        CancellationToken token
    )
    {
        using var response = await Client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false);

        _ = response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        var done = 0L;
        var buffer = new byte[81920];

        var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);

        await using (input.ConfigureAwait(false))
        {
            var output = File.Create(path);

            await using (output.ConfigureAwait(false))
            {
                while (true)
                {
                    var count = await input.ReadAsync(buffer, token).ConfigureAwait(false);

                    if (count == 0)
                    {
                        break;
                    }

                    await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    done += count;

                    if (total > 0)
                    {
                        progress?.Report(done * 100d / total);
                    }
                }
            }
        }
    }

    private static void RunSetup(string path)
    {
        _ = Process.Start(
            new ProcessStartInfo(path)
            {
                Arguments = "/SILENT /NORESTART /updated=1",
                UseShellExecute = true,
            }
        );
    }

    private static void SwapPortable(string staged)
    {
        var current = Environment.ProcessPath;

        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var command =
            $"ping -n 4 127.0.0.1 >nul & move /y \"{staged}\" \"{current}\" >nul & start \"\" \"{current}\"";

        _ = Process.Start(
            new ProcessStartInfo("cmd.exe")
            {
                Arguments = "/c " + command,
                CreateNoWindow = true,
                UseShellExecute = false,
            }
        );
    }
}

public sealed class UpdateCheckService(ISettingsService settings, IUpdateService updates) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!settings.Current.CheckForUpdates)
        {
            return Task.CompletedTask;
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken).ConfigureAwait(false);
                    _ = await updates.CheckAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                }
            },
            cancellationToken
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
