using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using ProjectAgil.Helpers;
using ProjectAgil.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ProjectAgil.ViewModels;

public partial class ToolsViewModel(
    INetworkService network,
    ISettingsService settings,
    IProcessRunner process,
    ISnackbarService snackbar
) : PageViewModel
{
    [ObservableProperty]
    private ObservableCollection<string> _output = [];

    [ObservableProperty]
    private string _traceHost = "mc.hypixel.net";

    public override Task OnNavigatedToAsync() => Task.CompletedTask;

    private void Log(string line) => OnUi(() => Output.Insert(0, $"{DateTime.Now:HH:mm:ss}  {line}"));

    private async Task RunToolAsync(string label, Func<Task<ProcessResult>> action, bool needsRestart = false)
    {
        IsBusy = true;
        BusyMessage = label;

        try
        {
            var result = await action().ConfigureAwait(false);

            Log(result.Success ? $"{label}: done" : $"{label}: {result.ShortError}");

            OnUi(() =>
                snackbar.Show(
                    result.Success ? label : $"{label} failed",
                    result.Success
                        ? needsRestart
                            ? "Done. A restart is needed for this to take effect."
                            : "Done."
                        : result.ShortError,
                    result.Success ? ControlAppearance.Success : ControlAppearance.Danger,
                    null,
                    TimeSpan.FromSeconds(5)
                )
            );
        }
        catch (Exception ex)
        {
            Log($"{label}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    [RelayCommand]
    private Task FlushDnsAsync() => RunToolAsync("Flush DNS cache", () => network.FlushDnsAsync());

    [RelayCommand]
    private Task ResetWinsockAsync() => RunToolAsync("Reset Winsock catalog", () => network.ResetWinsockAsync(), true);

    [RelayCommand]
    private Task ResetTcpIpAsync() => RunToolAsync("Reset TCP/IP stack", () => network.ResetTcpIpAsync(), true);

    [RelayCommand]
    private Task ClearArpAsync() => RunToolAsync("Clear ARP cache", () => network.ClearArpCacheAsync());

    [RelayCommand]
    private Task RenewLeaseAsync() => RunToolAsync("Release and renew DHCP lease", () => network.RenewLeaseAsync());

    [RelayCommand]
    private async Task TraceRouteAsync()
    {
        if (string.IsNullOrWhiteSpace(TraceHost))
        {
            return;
        }

        IsBusy = true;
        BusyMessage = $"Tracing route to {TraceHost}";

        try
        {
            var result = await process.RunAsync("tracert.exe", $"-d -h 20 -w 800 {TraceHost}").ConfigureAwait(false);

            foreach (var line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                Log(line);
            }
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task ExportReportAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text file|*.txt",
            FileName = $"agil-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsBusy = true;
        BusyMessage = "Building report";

        try
        {
            var adapter = network.GetPreferredAdapter(settings.Current.PreferredAdapterId);
            var report = await network.BuildReportAsync(adapter).ConfigureAwait(false);

            await File.WriteAllTextAsync(dialog.FileName, report).ConfigureAwait(false);

            Log($"Report written to {dialog.FileName}");

            OnUi(() =>
                snackbar.Show("Report saved", dialog.FileName, ControlAppearance.Success, null, TimeSpan.FromSeconds(5))
            );
        }
        catch (Exception ex)
        {
            Log($"Report failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            _ = Process.Start(new ProcessStartInfo { FileName = AppPaths.Root, UseShellExecute = true });
        }
        catch
        {
        }
    }

    [RelayCommand]
    private void ClearOutput() => Output.Clear();
}
