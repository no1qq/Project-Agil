using System.Diagnostics;
using System.Text;

namespace ProjectAgil.Services;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    private static readonly string[] ErrorStreamMarkers =
    [
        "is not recognized",
        "not supported",
        "invalid parameter",
        "cannotfind",
        "objectnotfound",
        "access is denied",
        "exception",
    ];

    private static readonly string[] OutputStreamMarkers =
    [
        "is not recognized",
        "not supported",
        "invalid parameter",
        "objectnotfound",
        "cannotfind",
    ];

    public bool Success
    {
        get
        {
            if (ExitCode != 0)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(StdErr)
                && ErrorStreamMarkers.Any(m => StdErr.Contains(m, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(StdOut)
                || !OutputStreamMarkers.Any(m => StdOut.Contains(m, StringComparison.OrdinalIgnoreCase));
        }
    }

    public string ShortError
    {
        get
        {
            var source = string.IsNullOrWhiteSpace(StdErr) ? StdOut : StdErr;
            if (string.IsNullOrWhiteSpace(source))
            {
                return $"exit code {ExitCode}";
            }

            var line = source
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(l => l.Length > 0);

            line ??= source.Trim();
            return line.Length > 160 ? line[..160] : line;
        }
    }
}

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default);

    Task<ProcessResult> RunPowerShellAsync(string command, CancellationToken ct = default);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        try
        {
            using var process = new Process { StartInfo = info };
            if (!process.Start())
            {
                return new ProcessResult(-1, string.Empty, "failed to start process");
            }

            var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stdErrTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            return new ProcessResult(
                process.ExitCode,
                await stdOutTask.ConfigureAwait(false),
                await stdErrTask.ConfigureAwait(false)
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ProcessResult(-1, string.Empty, ex.Message);
        }
    }

    public Task<ProcessResult> RunPowerShellAsync(string command, CancellationToken ct = default)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        return RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            ct
        );
    }
}
