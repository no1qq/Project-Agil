using ProjectAgil.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace ProjectAgil.Views.Pages;

public partial class ProfilesPage : INavigableView<ProfilesViewModel>
{
    public ProfilesPage(ProfilesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    public ProfilesViewModel ViewModel { get; }
}
