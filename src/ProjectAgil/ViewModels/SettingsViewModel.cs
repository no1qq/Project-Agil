using System.Diagnostics;
using System.IO;
using ProjectAgil.Helpers;
using ProjectAgil.Services;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ProjectAgil.ViewModels;

public partial class SettingsViewModel(
    ISettingsService settings,
    IStartupService startup,
    IElevationService elevation,
    IContentDialogService dialogs,
    ISnackbarService snackbar
) : PageViewModel
{
    private bool _loading;

    [ObservableProperty]
    private bool _isDark = true;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _closeToTray = true;

    [ObservableProperty]
    private bool _confirmBeforeApply = true;

    [ObservableProperty]
    private bool _autoSaveProfile = true;

    [ObservableProperty]
    private int _pingInterval = 500;

    [ObservableProperty]
    private int _pingTimeout = 1000;

    [ObservableProperty]
    private int _historySize = 240;

    [ObservableProperty]
    private int _benchmarkSamples = 20;

    [ObservableProperty]
    private bool _advancedMode;

    [ObservableProperty]
    private string _dataFolder = AppPaths.Root;

    [ObservableProperty]
    private bool _isElevated;

    public override async Task OnNavigatedToAsync()
    {
        _loading = true;

        var config = settings.Current;

        IsDark = !config.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase);
        StartMinimized = config.StartMinimized;
        MinimizeToTray = config.MinimizeToTray;
        CloseToTray = config.CloseToTray;
        ConfirmBeforeApply = config.ConfirmBeforeApply;
        AutoSaveProfile = config.AutoSaveProfile;
        PingInterval = config.PingIntervalMs;
        PingTimeout = config.PingTimeoutMs;
        HistorySize = config.HistorySize;
        BenchmarkSamples = config.BenchmarkSamples;
        AdvancedMode = config.AdvancedMode;
        IsElevated = elevation.IsElevated;

        StartWithWindows = await startup.IsEnabledAsync().ConfigureAwait(false);

        _loading = false;
    }

    private void Persist()
    {
        if (_loading)
        {
            return;
        }

        var config = settings.Current;

        config.Theme = IsDark ? "Dark" : "Light";
        config.StartMinimized = StartMinimized;
        config.MinimizeToTray = MinimizeToTray;
        config.CloseToTray = CloseToTray;
        config.ConfirmBeforeApply = ConfirmBeforeApply;
        config.AutoSaveProfile = AutoSaveProfile;
        config.PingIntervalMs = Math.Clamp(PingInterval, 100, 5000);
        config.PingTimeoutMs = Math.Clamp(PingTimeout, 200, 8000);
        config.HistorySize = Math.Clamp(HistorySize, 60, 2000);
        config.BenchmarkSamples = Math.Clamp(BenchmarkSamples, 12, 200);

        settings.Save();
    }

    partial void OnIsDarkChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        ApplicationThemeManager.Apply(value ? ApplicationTheme.Dark : ApplicationTheme.Light);
        Persist();
    }

    partial void OnStartMinimizedChanged(bool value) => Persist();

    partial void OnMinimizeToTrayChanged(bool value) => Persist();

    partial void OnCloseToTrayChanged(bool value) => Persist();

    partial void OnConfirmBeforeApplyChanged(bool value) => Persist();

    partial void OnAutoSaveProfileChanged(bool value) => Persist();

    partial void OnPingIntervalChanged(int value) => Persist();

    partial void OnPingTimeoutChanged(int value) => Persist();

    partial void OnHistorySizeChanged(int value) => Persist();

    partial void OnBenchmarkSamplesChanged(int value) => Persist();

    partial void OnAdvancedModeChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        settings.SetAdvancedMode(value);
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        _ = ToggleStartupAsync(value);
    }

    private async Task ToggleStartupAsync(bool enabled)
    {
        var ok = await startup.SetAsync(enabled).ConfigureAwait(false);

        settings.Current.StartWithWindows = enabled && ok;
        settings.Save();

        OnUi(() =>
            snackbar.Show(
                ok ? "Startup updated" : "Startup change failed",
                ok
                    ? enabled
                        ? "Project-Agil will start with Windows as an elevated scheduled task."
                        : "Project-Agil will no longer start with Windows."
                    : "The scheduled task could not be written.",
                ok ? ControlAppearance.Success : ControlAppearance.Danger,
                null,
                TimeSpan.FromSeconds(5)
            )
        );
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
    private async Task ResetDataAsync()
    {
        var confirmed = await dialogs.ConfirmAsync(
            "Delete local data",
            "Profiles, restore points and settings will be deleted. This does not undo changes already made to Windows, so revert them first if you want them undone.",
            "Delete"
        );

        if (!confirmed)
        {
            return;
        }

        try
        {
            foreach (var directory in new[] { AppPaths.Backups, AppPaths.Profiles, AppPaths.Logs })
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }

            if (File.Exists(AppPaths.SettingsFile))
            {
                File.Delete(AppPaths.SettingsFile);
            }

            if (File.Exists(AppPaths.ActiveProfileFile))
            {
                File.Delete(AppPaths.ActiveProfileFile);
            }

            AppPaths.EnsureCreated();

            snackbar.Show("Deleted", "Local data was removed.", ControlAppearance.Success, null, TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            snackbar.Show("Delete failed", ex.Message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand]
    private void OpenRepository()
    {
        try
        {
            _ = Process.Start(new ProcessStartInfo { FileName = "https://wpfui.lepo.co/", UseShellExecute = true });
        }
        catch
        {
        }
    }
}
