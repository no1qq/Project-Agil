using Microsoft.Win32;
using ProjectAgil.Models;
using ProjectAgil.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ProjectAgil.ViewModels;

public partial class ProfilesViewModel(IProfileService profiles, ISnackbarService snackbar) : PageViewModel
{
    [ObservableProperty]
    private ObservableCollection<OptimizationProfile> _saved = [];

    [ObservableProperty]
    private OptimizationProfile? _selected;

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _activeSummary = string.Empty;

    public override Task OnNavigatedToAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    private void Refresh()
    {
        Saved = [.. profiles.LoadAll()];
        ActiveSummary = $"{profiles.Active.Name}  -  {profiles.Active.Summary}";
    }

    [RelayCommand]
    private void SaveCurrent()
    {
        var name = string.IsNullOrWhiteSpace(NewName) ? profiles.Active.Name : NewName.Trim();

        var copy = profiles.Active.Clone();
        copy.Name = name;

        profiles.Save(copy);
        profiles.Active.Name = name;
        profiles.SaveActive();

        NewName = string.Empty;
        Refresh();

        snackbar.Show("Saved", $"Profile \"{name}\" was saved.", ControlAppearance.Success, null, TimeSpan.FromSeconds(4));
    }

    [RelayCommand]
    private void Load(OptimizationProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        profiles.Active = profile.Clone();
        profiles.SaveActive();
        Refresh();

        snackbar.Show(
            "Loaded",
            $"\"{profile.Name}\" is now the active profile. Open Optimize to run it.",
            ControlAppearance.Success,
            null,
            TimeSpan.FromSeconds(5)
        );
    }

    [RelayCommand]
    private void Update(OptimizationProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        var copy = profiles.Active.Clone();
        copy.Name = profile.Name;

        profiles.Save(copy);
        Refresh();

        snackbar.Show(
            "Updated",
            $"\"{profile.Name}\" now holds what you have on the Optimize page.",
            ControlAppearance.Success,
            null,
            TimeSpan.FromSeconds(4)
        );
    }

    [RelayCommand]
    private void Delete(OptimizationProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        profiles.Delete(profile.Name);
        Refresh();
    }

    [RelayCommand]
    private void Export(OptimizationProfile? profile)
    {
        var target = profile ?? profiles.Active;

        var dialog = new SaveFileDialog
        {
            Filter = "Project-Agil profile|*.json",
            FileName = $"{target.Name}.json",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            profiles.Export(target, dialog.FileName);
            snackbar.Show("Exported", dialog.FileName, ControlAppearance.Success, null, TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            snackbar.Show("Export failed", ex.Message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand]
    private void Import()
    {
        var dialog = new OpenFileDialog { Filter = "Project-Agil profile|*.json" };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var imported = profiles.Import(dialog.FileName);

        if (imported is null)
        {
            snackbar.Show("Import failed", "That file is not a valid profile.", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
            return;
        }

        profiles.Save(imported);
        Refresh();

        snackbar.Show("Imported", $"\"{imported.Name}\" was added.", ControlAppearance.Success, null, TimeSpan.FromSeconds(4));
    }
}
