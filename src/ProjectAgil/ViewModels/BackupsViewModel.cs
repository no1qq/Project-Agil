using ProjectAgil.Helpers;
using ProjectAgil.Models;
using ProjectAgil.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ProjectAgil.ViewModels;

public partial class BackupsViewModel(
    IBackupService backups,
    ITweakEngine engine,
    IContentDialogService dialogs,
    ISnackbarService snackbar
) : PageViewModel
{
    [ObservableProperty]
    private ObservableCollection<BackupSnapshot> _snapshots = [];

    [ObservableProperty]
    private BackupSnapshot? _selected;

    [ObservableProperty]
    private ObservableCollection<BackupEntry> _entries = [];

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private string _progressText = string.Empty;

    public override Task OnNavigatedToAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    private void Refresh()
    {
        var all = backups.LoadAll();
        var selectedId = Selected?.Id;

        Snapshots = [.. all];
        Selected = all.FirstOrDefault(s => s.Id == selectedId) ?? all.FirstOrDefault();
        ActiveCount = all.Count(s => !s.Restored);
    }

    partial void OnSelectedChanged(BackupSnapshot? value) => Entries = value is null ? [] : [.. value.Entries];

    [RelayCommand]
    private async Task RestoreAsync(BackupSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        var confirmed = await dialogs.ConfirmAsync(
            "Restore this point",
            $"{snapshot.EntryCount} settings will be put back to the values they had on {snapshot.CreatedDisplay}.",
            "Restore"
        );

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        BusyMessage = "Restoring";

        try
        {
            var progress = new Progress<EngineProgress>(p => ProgressText = $"{p.Current} of {p.Total}   {p.Message}");
            var restored = await engine.RestoreAsync(snapshot, progress).ConfigureAwait(false);

            OnUi(() =>
            {
                Refresh();
                ProgressText = string.Empty;
                snackbar.Show(
                    "Restored",
                    $"{restored} settings were put back.",
                    ControlAppearance.Success,
                    null,
                    TimeSpan.FromSeconds(5)
                );
            });
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task RestoreAllAsync()
    {
        var confirmed = await dialogs.ConfirmAsync(
            "Revert everything",
            "Every change Project-Agil has ever made on this machine will be put back to its original value.",
            "Revert everything"
        );

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        BusyMessage = "Reverting everything";

        try
        {
            var progress = new Progress<EngineProgress>(p => ProgressText = $"{p.Current} of {p.Total}   {p.Message}");
            var restored = await engine.RestoreAllAsync(progress).ConfigureAwait(false);

            OnUi(() =>
            {
                Refresh();
                ProgressText = string.Empty;
                snackbar.Show(
                    "Everything reverted",
                    $"{restored} settings were put back.",
                    ControlAppearance.Success,
                    null,
                    TimeSpan.FromSeconds(6)
                );
            });
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    [RelayCommand]
    private void Delete(BackupSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        backups.Delete(snapshot.Id);
        Refresh();
    }

    [RelayCommand]
    private void RefreshList() => Refresh();
}
