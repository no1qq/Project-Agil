using ProjectAgil.Models;
using ProjectAgil.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ProjectAgil.ViewModels;

public sealed class AdapterProperty(string keyword, string value)
{
    public string Keyword { get; } = keyword;

    public string Value { get; } = value;
}

public partial class AdaptersViewModel(
    INetworkService network,
    ISettingsService settings,
    ISnackbarService snackbar
) : PageViewModel
{
    [ObservableProperty]
    private ObservableCollection<NetworkAdapterInfo> _adapters = [];

    [ObservableProperty]
    private NetworkAdapterInfo? _selected;

    [ObservableProperty]
    private ObservableCollection<AdapterProperty> _properties = [];

    [ObservableProperty]
    private int _mtu = 1500;

    [ObservableProperty]
    private string _customDns = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    public override async Task OnNavigatedToAsync() => await RefreshAsync().ConfigureAwait(false);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        BusyMessage = "Reading adapters";

        try
        {
            var adapters = network.GetAdapters().ToList();
            var preferredId = Selected?.Id ?? settings.Current.PreferredAdapterId;
            var selected = adapters.FirstOrDefault(a => a.Id == preferredId) ?? adapters.FirstOrDefault(a => a.IsUp);

            OnUi(() =>
            {
                Adapters = [.. adapters];
                Selected = selected;
                Mtu = selected?.Mtu > 0 ? selected.Mtu : 1500;
            });

            await LoadPropertiesAsync(selected).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task LoadPropertiesAsync(NetworkAdapterInfo? adapter)
    {
        if (adapter is null)
        {
            OnUi(() => Properties = []);
            return;
        }

        var state = await network.ReadStateAsync(adapter).ConfigureAwait(false);

        var list = state
            .Where(p => p.Key.StartsWith("nic.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => new AdapterProperty(p.Key[4..], p.Value))
            .ToList();

        OnUi(() => Properties = [.. list]);
    }

    partial void OnSelectedChanged(NetworkAdapterInfo? value)
    {
        if (value is null)
        {
            return;
        }

        settings.Current.PreferredAdapterId = value.Id;
        settings.Save();

        Mtu = value.Mtu > 0 ? value.Mtu : 1500;

        _ = LoadPropertiesAsync(value);
    }

    [RelayCommand]
    private async Task ApplyMtuAsync()
    {
        if (Selected is null)
        {
            return;
        }

        var value = Math.Clamp(Mtu, 576, 9000);
        var result = await network.SetMtuAsync(Selected, value).ConfigureAwait(false);

        Report(result, $"MTU set to {value}");
    }

    [RelayCommand]
    private async Task SetDnsAsync(string preset)
    {
        if (Selected is null)
        {
            return;
        }

        string[] servers = preset switch
        {
            "cloudflare" => ["1.1.1.1", "1.0.0.1"],
            "google" => ["8.8.8.8", "8.8.4.4"],
            "quad9" => ["9.9.9.9", "149.112.112.112"],
            "adguard" => ["94.140.14.14", "94.140.15.15"],
            _ => [.. CustomDns.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
        };

        if (servers.Length == 0)
        {
            snackbar.Show("No servers", "Enter at least one DNS address.", ControlAppearance.Caution, null, TimeSpan.FromSeconds(4));
            return;
        }

        var result = await network.SetDnsAsync(Selected, servers).ConfigureAwait(false);
        Report(result, $"DNS set to {string.Join(", ", servers)}");

        await RefreshAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ResetDnsAsync()
    {
        if (Selected is null)
        {
            return;
        }

        var result = await network.ResetDnsAsync(Selected).ConfigureAwait(false);
        Report(result, "DNS reset to automatic");

        await RefreshAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RestartAdapterAsync()
    {
        if (Selected is null)
        {
            return;
        }

        IsBusy = true;
        BusyMessage = "Restarting adapter";

        try
        {
            var result = await network.RestartAdapterAsync(Selected).ConfigureAwait(false);
            Report(result, $"{Selected.Name} restarted");

            await Task.Delay(2500).ConfigureAwait(false);
            await RefreshAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private void Report(ProcessResult result, string success)
    {
        OnUi(() =>
        {
            Status = result.Success ? success : result.ShortError;

            snackbar.Show(
                result.Success ? "Done" : "Failed",
                Status,
                result.Success ? ControlAppearance.Success : ControlAppearance.Danger,
                null,
                TimeSpan.FromSeconds(5)
            );
        });
    }
}
